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
}

export default MoviesAppState;
