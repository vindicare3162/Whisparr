import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import Movie from 'Movie/Movie';

export type MovieEntity =
  | 'calendar'
  | 'movies'
  | 'interactiveImport.movies'
  | 'wanted.cutoffUnmet'
  | 'wanted.missing';

const EMPTY_ITEMS: Movie[] = [];

// Pages other than the movie/scene index no longer fetch the full catalog, so
// `state.movies` can be empty (and `itemMap` undefined) while a paged page such
// as Wanted has the movies it renders in its own section. Look the movie up in
// the section it was rendered from when the caller tells us which one that is.
function selectEntityItems(state: AppState, movieEntity: MovieEntity): Movie[] {
  switch (movieEntity) {
    case 'calendar':
      return state.calendar.items ?? EMPTY_ITEMS;
    case 'wanted.cutoffUnmet':
      return state.wanted.cutoffUnmet.items ?? EMPTY_ITEMS;
    case 'wanted.missing':
      return state.wanted.missing.items ?? EMPTY_ITEMS;
    default:
      return state.movies.items ?? EMPTY_ITEMS;
  }
}

export function createMovieSelector(
  movieId?: number,
  movieEntity: MovieEntity = 'movies'
) {
  return createSelector(
    (state: AppState) => selectEntityItems(state, movieEntity),
    (state: AppState) => state.movies.itemMap,
    (state: AppState) => state.movies.items,
    (entityItems, itemMap, allMovies): Movie | undefined => {
      if (!movieId) {
        return undefined;
      }

      if (movieEntity !== 'movies') {
        const movie = entityItems.find(({ id }) => id === movieId);

        if (movie) {
          return movie;
        }
      }

      const index = itemMap?.[movieId];

      return index === undefined ? undefined : allMovies?.[index];
    }
  );
}

function useMovie(movieId: number | undefined, movieEntity?: MovieEntity) {
  return useSelector(createMovieSelector(movieId, movieEntity));
}

export default useMovie;
