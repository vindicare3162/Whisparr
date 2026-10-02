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

export function createMovieSelector(movieId?: number) {
  return createSelector(
    (state: AppState) => state.movies.itemMap,
    (state: AppState) => state.movies.items,
    (itemMap, allMovies): Movie | undefined => {
      // The full catalog is only loaded by the index pages, so itemMap can be
      // undefined on every other page.
      if (!movieId || !itemMap) {
        return undefined;
      }

      const index = itemMap[movieId];

      return index === undefined ? undefined : allMovies[index];
    }
  );
}

function useMovie(movieId: number | undefined) {
  return useSelector(createMovieSelector(movieId));
}

export default useMovie;
