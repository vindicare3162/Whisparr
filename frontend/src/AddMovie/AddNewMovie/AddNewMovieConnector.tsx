import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import { clearAddMovie, lookupMovie } from 'Store/Actions/addMovieActions';
import { fetchMovieCount } from 'Store/Actions/movieActions';
import {
  clearMovieFiles,
  fetchMovieFiles,
} from 'Store/Actions/movieFileActions';
import {
  clearQueueDetails,
  fetchQueueDetails,
} from 'Store/Actions/queueActions';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import hasDifferentItems from 'Utilities/Object/hasDifferentItems';
import selectUniqueIds from 'Utilities/Object/selectUniqueIds';
import parseUrl from 'Utilities/String/parseUrl';
import AddNewMovie from './AddNewMovie';

interface AddMovieItem {
  id: number;
  internalId?: number;
  [key: string]: unknown;
}

interface AddNewMovieConnectorProps {
  term?: string;
  items: AddMovieItem[];
  lookupMovie: (payload: { term: string }) => void;
  clearAddMovie: () => void;
  fetchMovieCount: () => void;
  fetchRootFolders: () => void;
  fetchQueueDetails: () => void;
  clearQueueDetails: () => void;
  fetchMovieFiles: (payload: { movieId: number[] }) => void;
  clearMovieFiles: () => void;
}

function createMapStateToProps() {
  return createSelector(
    (state: AppState) => state.addMovie,
    (state: AppState) => state.movies.count,
    (state: AppState) => state.router.location,
    createUISettingsSelector(),
    (addMovie, existingMoviesCount, location, uiSettings) => {
      const { params } = parseUrl(location.search);

      return {
        ...addMovie,
        term: params.term,
        hasExistingMovies: (existingMoviesCount ?? 0) > 0,
        colorImpairedMode: uiSettings.enableColorImpairedMode,
      };
    }
  );
}

const mapDispatchToProps = {
  lookupMovie,
  clearAddMovie,
  fetchMovieCount,
  fetchRootFolders,
  fetchQueueDetails,
  clearQueueDetails,
  fetchMovieFiles,
  clearMovieFiles,
};

class AddNewMovieConnector extends Component<AddNewMovieConnectorProps> {
  //
  // Lifecycle

  componentDidMount() {
    this.props.fetchRootFolders();
    this.props.fetchQueueDetails();
    this.props.fetchMovieCount();
  }

  componentDidUpdate(prevProps: AddNewMovieConnectorProps) {
    const { items } = this.props;

    if (hasDifferentItems(prevProps.items, items)) {
      const movieIds = selectUniqueIds(items as never, 'internalId');

      if (movieIds.length) {
        this.props.fetchMovieFiles({ movieId: movieIds });
      }
    }
  }

  componentWillUnmount() {
    if (this._movieLookupTimeout) {
      clearTimeout(this._movieLookupTimeout);
    }

    this.props.clearAddMovie();
    this.props.clearQueueDetails();
    this.props.clearMovieFiles();
  }

  private _movieLookupTimeout: ReturnType<typeof setTimeout> | null = null;

  //
  // Listeners

  onMovieLookupChange = (term: string) => {
    if (this._movieLookupTimeout) {
      clearTimeout(this._movieLookupTimeout);
    }

    if (term.trim() === '') {
      this.props.clearAddMovie();
    } else {
      this._movieLookupTimeout = setTimeout(() => {
        this.props.lookupMovie({ term });
      }, 300);
    }
  };

  onClearMovieLookup = () => {
    this.props.clearAddMovie();
  };

  //
  // Render

  render() {
    const { term, ...otherProps } = this.props;

    return (
      <AddNewMovie
        term={term}
        {...otherProps}
        onMovieLookupChange={this.onMovieLookupChange}
        onClearMovieLookup={this.onClearMovieLookup}
      />
    );
  }
}

export default connect(
  createMapStateToProps,
  mapDispatchToProps
)(AddNewMovieConnector);
