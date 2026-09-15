namespace NzbDrone.Core.CustomFormats
{
    public class ReleaseTitleSpecification : RegexSpecificationBase
    {
        public override int Order => 1;
        public override string ImplementationName => "Release Title";
        public override string InfoLink => "https://wiki.servarr.com/whisparr/settings#custom-formats-2";

        protected override bool IsSatisfiedByWithoutNegate(CustomFormatInput input)
        {
            // SimpleReleaseTitle has the parsed title/episode-name portion replaced with a
            // generic placeholder (see Parser.ParseMovieTitle's release-group extraction step),
            // which for scene releases can swallow format-relevant tokens like "iMAGESET" that
            // happen to fall inside whatever the matching regex captured as the "title" group.
            // Fall back to the raw ReleaseTitle so custom formats can still match those tokens.
            return MatchString(input.MovieInfo?.SimpleReleaseTitle) ||
                   MatchString(input.MovieInfo?.ReleaseTitle) ||
                   MatchString(input.Filename);
        }
    }
}
