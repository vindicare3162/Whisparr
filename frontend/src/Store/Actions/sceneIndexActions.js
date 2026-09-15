import _ from 'lodash';
import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { filterBuilderTypes, filterBuilderValueTypes, filterTypes, sortDirections } from 'Helpers/Props';
import { createThunk, handleThunks } from 'Store/thunks';
import sortByProp from 'Utilities/Array/sortByProp';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import findSelectedFilters from 'Utilities/Filter/findSelectedFilters';
import serverSideCollectionHandlers from 'Utilities/serverSideCollectionHandlers';
import getSectionState from 'Utilities/State/getSectionState';
import translate from 'Utilities/String/translate';
import { set, update, updateServerSideCollection } from './baseActions';
import createHandleActions from './Creators/createHandleActions';
import createServerSideCollectionHandlers from './Creators/createServerSideCollectionHandlers';
import createSetTableOptionReducer from './Creators/Reducers/createSetTableOptionReducer';

//
// Variables

export const section = 'sceneIndex';

//
// State

export const defaultState = {
  isFetching: false,
  isPopulated: false,
  error: null,
  pageSize: 100,
  page: 1,
  totalPages: 0,
  totalRecords: 0,
  isSaving: false,
  saveError: null,
  isDeleting: false,
  deleteError: null,
  indexMode: 'scene',
  sortKey: 'sortTitle',
  sortDirection: sortDirections.ASCENDING,
  secondarySortKey: 'sortTitle',
  secondarySortDirection: sortDirections.ASCENDING,
  view: 'posters',

  posterOptions: {
    detailedProgressBar: false,
    size: 'large',
    showTitle: false,
    showMonitored: true,
    showQualityProfile: true,
    showReleaseDate: false,
    showTmdbRating: false,
    showSearchAction: false
  },

  overviewOptions: {
    detailedProgressBar: false,
    size: 'medium',
    showMonitored: true,
    showStudio: true,
    showQualityProfile: true,
    showAdded: false,
    showPath: false,
    showSizeOnDisk: false,
    showSearchAction: false
  },

  tableOptions: {
    showSearchAction: false
  },

  columns: [
    {
      name: 'select',
      columnLabel: 'Select',
      isSortable: false,
      isVisible: true,
      isModifiable: false,
      isHidden: true
    },
    {
      name: 'status',
      columnLabel: () => translate('ReleaseStatus'),
      isSortable: false,
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'sortTitle',
      label: () => translate('SceneTitle'),
      isSortable: true,
      isVisible: true,
      isModifiable: false
    },
    {
      name: 'studio',
      label: () => translate('Studio'),
      isSortable: true,
      isVisible: true
    },
    {
      name: 'qualityProfileId',
      label: () => translate('QualityProfile'),
      isSortable: true,
      isVisible: true
    },
    {
      name: 'added',
      label: () => translate('Added'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'year',
      label: () => translate('Year'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'releaseDate',
      label: () => translate('ReleaseDate'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'runtime',
      label: () => translate('Runtime'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'path',
      label: () => translate('Path'),
      isSortable: true,
      isVisible: false
    },
    {
      name: 'sizeOnDisk',
      label: () => translate('SizeOnDisk'),
      isSortable: false,
      isVisible: false
    },
    {
      name: 'genres',
      label: () => translate('Genres'),
      isSortable: false,
      isVisible: false
    },
    {
      name: 'movieStatus',
      label: () => translate('Status'),
      isSortable: false,
      isVisible: true
    },
    {
      name: 'tags',
      label: () => translate('Tags'),
      isSortable: false,
      isVisible: false
    },
    {
      name: 'actions',
      columnLabel: () => translate('Actions'),
      isVisible: true,
      isModifiable: false
    }
  ],

  selectedFilterKey: 'all',

  // Preset filters are translated by the fetch handler into server-side query
  // params (monitored/hasFile/itemType) for GET /movie/paged.
  filters: [
    {
      key: 'all',
      label: () => translate('All'),
      filters: []
    },
    {
      key: 'monitored',
      label: () => translate('MonitoredOnly'),
      filters: [
        {
          key: 'monitored',
          value: true,
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'unmonitored',
      label: () => translate('Unmonitored'),
      filters: [
        {
          key: 'monitored',
          value: false,
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'missing',
      label: () => translate('Missing'),
      filters: [
        {
          key: 'monitored',
          value: true,
          type: filterTypes.EQUAL
        },
        {
          key: 'hasFile',
          value: false,
          type: filterTypes.EQUAL
        }
      ]
    },
    {
      key: 'downloaded',
      label: () => translate('Downloaded'),
      filters: [
        {
          key: 'hasFile',
          value: true,
          type: filterTypes.EQUAL
        }
      ]
    }
  ],

  // EQUAL-only server-supported filter builder props. Predicates such as
  // ranges or "contains" are not expressible in the flattened query params
  // and are intentionally not offered in the pilot (issue #37).
  filterBuilderProps: [
    {
      name: 'monitored',
      label: () => translate('Monitored'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.BOOL
    },
    {
      name: 'year',
      label: () => translate('Year'),
      type: filterBuilderTypes.EXACT
    },
    {
      name: 'added',
      label: () => translate('Added'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.DATE
    },
    {
      name: 'releaseDate',
      label: () => translate('ReleaseDate'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.DATE
    },
    {
      name: 'studioTitle',
      label: () => translate('Studio'),
      type: filterBuilderTypes.EXACT,
      optionsSelector: function(items) {
        const tagList = (items || []).reduce((acc, scene) => {
          if (scene && scene.studioTitle) {
            acc.push({
              id: scene.studioTitle,
              name: scene.studioTitle
            });
          }

          return acc;
        }, []);

        const tags = _.uniqBy(tagList, 'id');

        return tags.sort(sortByProp('name'));
      }
    },
    {
      name: 'qualityProfileId',
      label: () => translate('QualityProfile'),
      type: filterBuilderTypes.EXACT,
      valueType: filterBuilderValueTypes.QUALITY_PROFILE
    }
  ]
};

export const persistState = [
  'sceneIndex.sortKey',
  'sceneIndex.sortDirection',
  'sceneIndex.selectedFilterKey',
  'sceneIndex.view',
  'sceneIndex.columns',
  'sceneIndex.pageSize',
  'sceneIndex.posterOptions',
  'sceneIndex.overviewOptions',
  'sceneIndex.tableOptions'
];

//
// Actions Types

export const FETCH_SCENE_INDEX = 'sceneIndex/fetchSceneIndex';
export const GOTO_FIRST_SCENE_PAGE = 'sceneIndex/gotoFirstScenePage';
export const GOTO_PREVIOUS_SCENE_PAGE = 'sceneIndex/gotoPreviousScenePage';
export const GOTO_NEXT_SCENE_PAGE = 'sceneIndex/gotoNextScenePage';
export const GOTO_LAST_SCENE_PAGE = 'sceneIndex/gotoLastScenePage';
export const GOTO_SCENE_PAGE = 'sceneIndex/gotoScenePage';
export const SET_MOVIE_SORT = 'sceneIndex/setSceneSort';
export const SET_MOVIE_FILTER = 'sceneIndex/setSceneFilter';
export const SET_MOVIE_VIEW = 'sceneIndex/setSceneView';
export const SET_MOVIE_TABLE_OPTION = 'sceneIndex/setSceneTableOption';
export const SET_MOVIE_POSTER_OPTION = 'sceneIndex/setScenePosterOption';
export const SET_MOVIE_OVERVIEW_OPTION = 'sceneIndex/setSceneOverviewOption';
export const SET_MOVIE_INDEX_MODE = 'sceneIndex/setSceneIndexMode';

//
// Action Creators

export const fetchSceneIndex = createThunk(FETCH_SCENE_INDEX);
export const gotoFirstScenePage = createThunk(GOTO_FIRST_SCENE_PAGE);
export const gotoPreviousScenePage = createThunk(GOTO_PREVIOUS_SCENE_PAGE);
export const gotoNextScenePage = createThunk(GOTO_NEXT_SCENE_PAGE);
export const gotoLastScenePage = createThunk(GOTO_LAST_SCENE_PAGE);
export const gotoScenePage = createThunk(GOTO_SCENE_PAGE);
export const setSceneSort = createThunk(SET_MOVIE_SORT);
export const setSceneFilter = createThunk(SET_MOVIE_FILTER);
export const setSceneView = createAction(SET_MOVIE_VIEW);
export const setSceneTableOption = createAction(SET_MOVIE_TABLE_OPTION);
export const setScenePosterOption = createAction(SET_MOVIE_POSTER_OPTION);
export const setSceneOverviewOption = createAction(SET_MOVIE_OVERVIEW_OPTION);
export const setSceneIndexMode = createAction(SET_MOVIE_INDEX_MODE);

//
// Action Handlers

function createFetchSceneIndexHandler(sectionName, url) {
  // Mirrors createFetchServerSideCollectionHandler, but additionally merges
  // the fetched records into the movie catalog so scene rows/panels (which
  // read movies via createMovieSelectorForHook) resolve (issue #37 pilot).
  return function(getState, payload, dispatch) {
    dispatch(set({ section: sectionName, isFetching: true }));

    const sectionState = getSectionState(getState(), sectionName, true);
    const page = payload.page || sectionState.page || 1;

    const data = Object.assign(
      { page },
      _.pick(sectionState, ['pageSize', 'sortDirection', 'sortKey'])
    );

    // The scene index only ever shows scenes; the movie/scene split is
    // filtered server-side.
    data.itemType = 'scene';

    const { selectedFilterKey, filters } = sectionState;

    const selectedFilters = findSelectedFilters(selectedFilterKey, filters, []);

    selectedFilters.forEach((filter) => {
      data[filter.key] = filter.value;
    });

    const { request, abortRequest } = createAjaxRequest({
      url,
      data,
      traditional: true
    });

    request.done((response) => {
      dispatch(
        batchActions([
          updateServerSideCollection({ section: sectionName, data: response }),

          // Merge the page records into the movie catalog so scene rows
          // resolve from state.movies.items.
          update({ section: 'movies', data: response.records }),

          set({
            section: sectionName,
            isFetching: false,
            isPopulated: true,
            error: null
          })
        ])
      );
    });

    request.fail((xhr) => {
      dispatch(
        set({
          section: sectionName,
          isFetching: false,
          isPopulated: false,
          error: xhr
        })
      );
    });

    return abortRequest;
  };
}

export const actionHandlers = handleThunks({
  ...createServerSideCollectionHandlers(
    section,
    '/movie/paged',
    fetchSceneIndex,
    {
      [serverSideCollectionHandlers.FETCH]: FETCH_SCENE_INDEX,
      [serverSideCollectionHandlers.FIRST_PAGE]: GOTO_FIRST_SCENE_PAGE,
      [serverSideCollectionHandlers.PREVIOUS_PAGE]: GOTO_PREVIOUS_SCENE_PAGE,
      [serverSideCollectionHandlers.NEXT_PAGE]: GOTO_NEXT_SCENE_PAGE,
      [serverSideCollectionHandlers.LAST_PAGE]: GOTO_LAST_SCENE_PAGE,
      [serverSideCollectionHandlers.EXACT_PAGE]: GOTO_SCENE_PAGE,
      [serverSideCollectionHandlers.SORT]: SET_MOVIE_SORT,
      [serverSideCollectionHandlers.FILTER]: SET_MOVIE_FILTER
    }
  ),

  // Custom fetch: also merges page records into the movie catalog (see above).
  [FETCH_SCENE_INDEX]: createFetchSceneIndexHandler(section, '/movie/paged')
});

//
// Reducers

export const reducers = createHandleActions({

  [SET_MOVIE_VIEW]: function(state, { payload }) {
    return Object.assign({}, state, { view: payload.view });
  },

  [SET_MOVIE_TABLE_OPTION]: createSetTableOptionReducer(section),

  [SET_MOVIE_POSTER_OPTION]: function(state, { payload }) {
    const posterOptions = state.posterOptions;

    return {
      ...state,
      posterOptions: {
        ...posterOptions,
        ...payload
      }
    };
  },

  [SET_MOVIE_OVERVIEW_OPTION]: function(state, { payload }) {
    const overviewOptions = state.overviewOptions;

    return {
      ...state,
      overviewOptions: {
        ...overviewOptions,
        ...payload
      }
    };
  },

  [SET_MOVIE_INDEX_MODE]: function(state, { payload }) {
    return Object.assign({}, state, { indexMode: payload.indexMode });
  }

}, defaultState, section);
