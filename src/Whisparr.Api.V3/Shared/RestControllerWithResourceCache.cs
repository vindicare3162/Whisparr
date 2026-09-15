using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.SignalR;
using Whisparr.Http.REST;

namespace Whisparr.Api.V3.Shared
{
    /// <summary>
    /// Base class for controllers that serve stash-backed resources (performers, studios) through
    /// an in-memory resource cache keyed by foreign ID. The cache-fill algorithm lives here once:
    /// bounded-wait stampede protection, negative caching of unknown foreign IDs, and fill work
    /// (cover URL conversion, movie count linkage) scoped to newly built resources only.
    /// </summary>
    public abstract class RestControllerWithResourceCache<TResource, TModel> : RestControllerWithSignalR<TResource, TModel>
        where TResource : RestResource, new()
        where TModel : ModelBase, new()
    {
        // A full-list fetch almost always misses the whole cache on cold start, so this threshold
        // is routinely exceeded; it exists only to serialize concurrent cold-start fills.
        private const int StampedeProtectionThreshold = 100;

        // Never block a request thread indefinitely waiting for a fill slot; if the lock cannot
        // be acquired in time, degrade gracefully and fill without it.
        private static readonly TimeSpan LockAcquireTimeout = TimeSpan.FromSeconds(10);

        private readonly ICached<TResource> _resourceCache;
        private readonly Logger _logger;

        protected RestControllerWithResourceCache(ICached<TResource> resourceCache, Logger logger, IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _resourceCache = resourceCache;
            _logger = logger;
        }

        /// <summary>
        /// Remove one foreign ID from the resource cache. Call after mutating or deleting a
        /// resource, and after adding a resource that may previously have been negative-cached
        /// (see below).
        /// </summary>
        protected void InvalidateCachedResource(string foreignId)
        {
            if (foreignId.IsNotNullOrWhiteSpace())
            {
                _resourceCache.Remove(foreignId);
            }
        }

        /// <summary>
        /// Clear the whole resource cache. Refilling is cheap (a handful of batched queries), so
        /// movie-library events (file added/deleted/updated, movies deleted/imported) simply clear
        /// the cache instead of trying to map a movie to the potentially many performers/studios
        /// it links to via credits.
        /// </summary>
        protected void InvalidateAllCachedResources()
        {
            _resourceCache.Clear();
        }

        protected TResource GetCachedResource(string foreignId)
        {
            return GetCachedResources(new List<string> { foreignId }).FirstOrDefault();
        }

        protected List<TResource> GetCachedResources(List<string> foreignIds)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.Trace("GetCachedResources: {0} resources", foreignIds.Count);

            var results = new List<TResource>(foreignIds.Count);
            var missingIds = new List<string>();

            CollectKnownResources(foreignIds, results, missingIds);

            if (missingIds.Count > 0)
            {
                var releaseLock = false;
                var getIds = missingIds;

                try
                {
                    _logger.Debug("Fetching {0} uncached resources ({1} already cached)", missingIds.Count, results.Count);

                    // Serialize large fills so concurrent cold-start requests don't each build the
                    // full list. The cache lock is a counting semaphore (not exclusive), so this
                    // bounds concurrent fill work rather than fully excluding readers.
                    if (missingIds.Count > StampedeProtectionThreshold)
                    {
                        releaseLock = _resourceCache.Lock.Wait(LockAcquireTimeout);

                        if (!releaseLock)
                        {
                            _logger.Warn("Could not acquire the resource cache lock within {0:F0} seconds; filling {1} resources without it",
                                LockAcquireTimeout.TotalSeconds,
                                missingIds.Count);
                        }
                        else
                        {
                            if (stopwatch.Elapsed.TotalSeconds > 2)
                            {
                                _logger.Warn("Waited {0:F1} seconds for the resource cache lock", stopwatch.Elapsed.TotalSeconds);
                            }

                            // Re-check after acquiring the lock: another request may have filled
                            // these while we waited.
                            getIds = new List<string>();
                            CollectKnownResources(missingIds, results, getIds);
                        }
                    }

                    if (getIds.Count > 0)
                    {
                        var newResources = BuildResources(getIds).Where(x => x != null).ToList();

                        // Fill work runs only for newly built resources; entries served from the
                        // cache were converted/linked when they were first built.
                        ConvertToLocalUrls(newResources);
                        LinkMovies(newResources);

                        var foundForeignIds = new HashSet<string>(newResources.Select(GetForeignId), StringComparer.OrdinalIgnoreCase);

                        foreach (var resource in newResources)
                        {
                            results.Add(resource);
                            _resourceCache.Set(GetForeignId(resource), resource);
                        }

                        // Negative-cache foreign IDs that do not exist in the library (e.g.
                        // performer IDs left behind in credits after a performer is deleted) so
                        // they are not re-queried on every request. A default TResource (with no
                        // foreign ID) marks the miss; it is replaced by a real entry when the
                        // resource is later added (controllers invalidate on add).
                        foreach (var id in getIds.Where(id => !foundForeignIds.Contains(id)))
                        {
                            _resourceCache.Set(id, new TResource());
                        }
                    }
                }
                finally
                {
                    stopwatch.Stop();

                    if (releaseLock)
                    {
                        _resourceCache.Lock.Release();
                    }
                }
            }

            if (stopwatch.Elapsed.TotalSeconds > 60)
            {
                _logger.Warn("Processed {0} resources ({1} cache misses) in {2:F1} seconds",
                    foreignIds.Count,
                    foreignIds.Count - results.Count,
                    stopwatch.Elapsed.TotalSeconds);
            }

            return results;
        }

        private void CollectKnownResources(List<string> foreignIds, List<TResource> results, List<string> missingIds)
        {
            foreach (var id in foreignIds)
            {
                var resource = _resourceCache.Find(id);

                if (resource == null)
                {
                    missingIds.Add(id);
                }
                else if (GetForeignId(resource).IsNullOrWhiteSpace())
                {
                    // Negative cache entry - known not to exist in the library.
                }
                else
                {
                    results.Add(resource);
                }
            }
        }

        /// <summary>All foreign IDs currently in the library (used to fill the full list from cache).</summary>
        protected abstract List<string> AllResourceForeignIds();

        /// <summary>Look up the given foreign IDs and translate them to resources (cache misses only).</summary>
        protected abstract List<TResource> BuildResources(List<string> foreignIds);

        /// <summary>Rewrite remote cover/image URLs to local ones for the given (newly built) resources.</summary>
        protected abstract void ConvertToLocalUrls(List<TResource> newResources);

        /// <summary>Populate movie/scene counts and sizes for the given (newly built) resources.</summary>
        protected abstract void LinkMovies(List<TResource> newResources);

        protected abstract string GetForeignId(TResource resource);
    }
}
