import AppSectionState, {
  AppSectionDeleteState,
  AppSectionSaveState,
} from 'App/State/AppSectionState';
import Column from 'Components/Table/Column';
import SortDirection from 'Helpers/Props/SortDirection';
import Movie from 'Movie/Movie';
import { Filter, FilterBuilderProp } from './AppState';

export interface MovieIndexAppState {
  sortKey: string;
  sortDirection: SortDirection;
  secondarySortKey: string;
  secondarySortDirection: SortDirection;
  view: string;

  posterOptions: {
    detailedProgressBar: boolean;
    size: string;
    showTitle: boolean;
    showMonitored: boolean;
    showQualityProfile: boolean;
    showReleaseDate: boolean;
    showTmdbRating: boolean;
    showTags: boolean;
    showSearchAction: boolean;
  };

  overviewOptions: {
    detailedProgressBar: boolean;
    size: string;
    showMonitored: boolean;
    showStudio: boolean;
    showQualityProfile: boolean;
    showAdded: boolean;
    showPath: boolean;
    showSizeOnDisk: boolean;
    showTags: boolean;
    showSearchAction: boolean;
  };

  tableOptions: {
    showSearchAction: boolean;
  };

  selectedFilterKey: string;
  filterBuilderProps: FilterBuilderProp<Movie>[];
  filters: Filter[];
  columns: Column[];
}

// Server-side collection state for the Scenes index pilot (issue #37).
export interface SceneIndexAppState extends MovieIndexAppState {
  isFetching: boolean;
  isPopulated: boolean;
  error?: unknown;
  pageSize: number;
  page: number;
  totalPages: number;
  totalRecords: number;
  items: Movie[];

  // Library-wide aggregates honoring the active filters (issue #37).
  stats?: {
    totalRecords: number;
    hasFileCount: number;
    monitoredCount: number;
    sizeOnDisk: number;
  };

  // First-letter jump bar positions within the filtered+sorted set.
  jumpBar?: Array<{ letter: string; index: number; count: number }>;
}

interface MoviesAppState
  extends AppSectionState<Movie>,
    AppSectionDeleteState,
    AppSectionSaveState {
  itemMap: Record<number, number>;

  deleteOptions: {
    addImportExclusion: boolean;
  };

  pendingChanges: Partial<Movie>;

  // Cache of movies keyed by performer foreignId, populated on-demand by the
  // performer detail page so it doesn't have to download the full catalog.
  performerMovies: Record<string, Movie[]>;

  // Cache of movies keyed by studio foreignId, populated on-demand by the
  // studio detail page so it doesn't have to download the full catalog.
  studioMovies: Record<string, Movie[]>;

  // Lightweight total from GET /movie/count, fetched on demand (e.g. by the
  // Add New pages). null until first fetched.
  count: number | null;

  // Per-titleSlug request state for GET /movie/detail/{titleSlug}, used by
  // the movie/scene detail pages so they don't need the full catalog.
  detailRequests: Record<
    string,
    { isFetching: boolean; isPopulated: boolean; error: unknown }
  >;
}

export default MoviesAppState;
