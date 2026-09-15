import { Error } from './AppSectionState';

interface AddMovieAppState {
  isFetching: boolean;
  isPopulated: boolean;
  error?: Error;
  isAdding: boolean;
  isAdded: boolean;
  addError?: Error;
  items: Array<{ id: number; internalId?: number; [key: string]: unknown }>;
}

export default AddMovieAppState;
