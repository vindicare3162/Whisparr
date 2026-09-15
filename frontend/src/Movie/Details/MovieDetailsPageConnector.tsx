import { push } from 'connected-react-router';
import _ from 'lodash';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { Error } from 'App/State/AppSectionState';
import AppState from 'App/State/AppState';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import NotFound from 'Components/NotFound';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { fetchMovieDetail } from 'Store/Actions/movieActions';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import MovieDetailsConnector from './MovieDetailsConnector';
import styles from './MovieDetails.css';

interface MatchParams {
  titleSlug: string;
}

interface MovieDetailsPageProps {
  match: { params: MatchParams };

  // Set when the movie/scene is found in the store; null while loading or
  // when the detail request did not find it.
  titleSlug: string | null;
  itemType?: string;
  isFetching: boolean;
  isPopulated: boolean;
  error?: Error;
  push: (path: string) => void;
  fetchRootFolders: () => void;
  fetchMovieDetail: (payload: { titleSlug: string }) => void;
}

interface MatchProps {
  match: { params: MatchParams };
}

function createMapStateToProps() {
  return createSelector(
    (_state: AppState, ownProps: MatchProps) => ownProps.match,
    (state: AppState) => state.movies,
    (match, movies) => {
      const titleSlug = match.params.titleSlug;
      const { isFetching, isPopulated, items, detailRequests } = movies;

      const movieIndex = _.findIndex(items, { titleSlug });

      if (movieIndex > -1) {
        const itemType = items[movieIndex].itemType;
        return {
          isFetching,
          isPopulated,
          itemType,
          titleSlug,
        };
      }

      // The full catalog isn't loaded; fall back to the per-slug detail
      // request state (see FETCH_MOVIE_DETAIL).
      const detail = detailRequests[titleSlug] || {
        isFetching: false,
        isPopulated: false,
        error: undefined,
      };

      return {
        isFetching: detail.isFetching,
        isPopulated: detail.isPopulated,
        error: detail.error as Error | undefined,
        titleSlug: null,
      };
    }
  );
}

const mapDispatchToProps = {
  push,
  fetchRootFolders,
  fetchMovieDetail,
};

class MovieDetailsPageConnector extends Component<MovieDetailsPageProps> {
  //
  // Lifecycle

  componentDidMount() {
    this.props.fetchRootFolders();
    this.fetchDetail();
  }

  componentDidUpdate(prevProps: MovieDetailsPageProps) {
    const { match } = this.props;

    if (prevProps.match.params.titleSlug !== match.params.titleSlug) {
      this.fetchDetail();
    }

    if (!this.props.titleSlug) {
      // Only redirect away once loading has actually finished and the
      // movie/scene isn't in the store - while a detail request is in
      // flight (or failed with an error) the loading/error states render.
      if (this.props.isFetching || !!this.props.error) {
        return;
      }

      if (!this.props.isPopulated) {
        return;
      }

      if (prevProps.itemType === 'scene') {
        this.props.push(`${window.Whisparr.urlBase}/`);
      } else {
        this.props.push(`${window.Whisparr.urlBase}/movies`);
      }
      return;
    }
  }

  fetchDetail() {
    const { titleSlug } = this.props.match.params;

    if (titleSlug) {
      this.props.fetchMovieDetail({ titleSlug });
    }
  }

  //
  // Render

  render() {
    const { titleSlug, isFetching, isPopulated, error } = this.props;

    if (isFetching && !isPopulated) {
      return (
        <PageContent title={translate('Loading')}>
          <PageContentBody>
            <LoadingIndicator />
          </PageContentBody>
        </PageContent>
      );
    }

    if (!isFetching && !!error) {
      return (
        <div className={styles.errorMessage}>
          {getErrorMessage(error, translate('FailedToLoadMovieFromAPI'))}
        </div>
      );
    }

    if (!titleSlug) {
      return <NotFound message={translate('SorryThatMovieCannotBeFound')} />;
    }

    return <MovieDetailsConnector titleSlug={titleSlug} />;
  }
}

export default connect(
  createMapStateToProps,
  mapDispatchToProps
)(MovieDetailsPageConnector);
