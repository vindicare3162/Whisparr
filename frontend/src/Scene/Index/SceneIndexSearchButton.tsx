import React, { useCallback, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useSelect } from 'App/SelectContext';
import AppState from 'App/State/AppState';
import { MOVIE_SEARCH } from 'Commands/commandNames';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import { icons, kinds } from 'Helpers/Props';
import { executeCommand } from 'Store/Actions/commandActions';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';

interface SceneIndexSearchButtonProps {
  isSelectMode: boolean;
  selectedFilterKey: string;
}

// Map filter preset keys to server-side search params (issue #37).
const FILTER_PARAMS: Record<
  string,
  { monitored?: boolean; hasFile?: boolean }
> = {
  monitored: { monitored: true },
  unmonitored: { monitored: false },
  missing: { monitored: true, hasFile: false },
  downloaded: { hasFile: true },
};

function SceneIndexSearchButton(props: SceneIndexSearchButtonProps) {
  const isSearching = useSelector(createCommandExecutingSelector(MOVIE_SEARCH));
  const { items, totalRecords }: AppState['sceneIndex'] = useSelector(
    (state: AppState) => state.sceneIndex
  );

  const dispatch = useDispatch();
  const [isConfirmModalOpen, setIsConfirmModalOpen] = useState(false);

  const { isSelectMode, selectedFilterKey } = props;
  const [selectState] = useSelect();
  const { selectedState } = selectState;

  const selectedSceneIds = useMemo(() => {
    return getSelectedIds(selectedState);
  }, [selectedState]);

  // Server-side filter search (issue #37): the search dispatches filter
  // params instead of page-scoped IDs so the backend searches all matching
  // scenes without requiring the full catalog client-side.
  const searchIndexLabel =
    selectedFilterKey === 'all'
      ? translate('SearchAll')
      : translate('SearchFiltered');

  const searchSelectLabel =
    selectedSceneIds.length > 0
      ? translate('SearchSelected')
      : translate('SearchAll');

  const onPress = useCallback(() => {
    setIsConfirmModalOpen(false);

    if (isSelectMode && selectedSceneIds.length > 0) {
      // Select mode: search the explicitly selected scenes.
      dispatch(
        executeCommand({
          name: MOVIE_SEARCH,
          movieIds: selectedSceneIds,
        })
      );
    } else {
      // Non-select mode: server-side filter search - the backend searches
      // all matching scenes without requiring the catalog client-side.
      const preset = FILTER_PARAMS[selectedFilterKey] ?? {};

      dispatch(
        executeCommand({
          name: MOVIE_SEARCH,
          itemType: 'scene',
          ...preset,
        })
      );
    }
  }, [dispatch, isSelectMode, selectedSceneIds, selectedFilterKey]);

  const onConfirmPress = useCallback(() => {
    setIsConfirmModalOpen(true);
  }, [setIsConfirmModalOpen]);

  const onConfirmModalClose = useCallback(() => {
    setIsConfirmModalOpen(false);
  }, [setIsConfirmModalOpen]);

  return (
    <>
      <PageToolbarButton
        label={isSelectMode ? searchSelectLabel : searchIndexLabel}
        isSpinning={isSearching}
        isDisabled={!items.length && !totalRecords}
        iconName={icons.SEARCH}
        onPress={
          isSelectMode && selectedSceneIds.length > 5 ? onConfirmPress : onPress
        }
      />

      <ConfirmModal
        isOpen={isConfirmModalOpen}
        kind={kinds.DANGER}
        title={isSelectMode ? searchSelectLabel : searchIndexLabel}
        message={translate('SearchMoviesConfirmationMessageText', {
          count: totalRecords ?? 0,
        })}
        confirmLabel={isSelectMode ? searchSelectLabel : searchIndexLabel}
        onConfirm={onPress}
        onCancel={onConfirmModalClose}
      />
    </>
  );
}

export default SceneIndexSearchButton;
