import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import { clearAddMovie, lookupPerformer } from 'Store/Actions/addMovieActions';
import { fetchMovieCount } from 'Store/Actions/movieActions';
import {
  clearQueueDetails,
  fetchQueueDetails,
} from 'Store/Actions/queueActions';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import parseUrl from 'Utilities/String/parseUrl';
import AddNewPerformer from './AddNewScene';

interface AddNewPerformerConnectorProps {
  term?: string;
  items: Array<{ id: number; [key: string]: unknown }>;
  lookupPerformer: (payload: { term: string }) => void;
  clearAddMovie: () => void;
  fetchMovieCount: () => void;
  fetchRootFolders: () => void;
  fetchQueueDetails: () => void;
  clearQueueDetails: () => void;
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
  lookupPerformer,
  clearAddMovie,
  fetchMovieCount,
  fetchRootFolders,
  fetchQueueDetails,
  clearQueueDetails,
};

class AddNewPerformerConnector extends Component<AddNewPerformerConnectorProps> {
  //
  // Lifecycle

  componentDidMount() {
    this.props.fetchRootFolders();
    this.props.fetchQueueDetails();
    this.props.fetchMovieCount();
  }

  componentWillUnmount() {
    if (this._movieLookupTimeout) {
      clearTimeout(this._movieLookupTimeout);
    }

    this.props.clearAddMovie();
    this.props.clearQueueDetails();
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
        this.props.lookupPerformer({ term });
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
      <AddNewPerformer
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
)(AddNewPerformerConnector);
