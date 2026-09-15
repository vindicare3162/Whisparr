import React, { useCallback, useMemo } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { useSelect } from 'App/SelectContext';
import AppState from 'App/State/AppState';
import { REFRESH_MOVIE } from 'Commands/commandNames';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import { icons } from 'Helpers/Props';
import { executeCommand } from 'Store/Actions/commandActions';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';

interface SceneIndexRefreshSceneButtonProps {
  isSelectMode: boolean;
  selectedFilterKey: string;
}

function SceneIndexRefreshSceneButton(
  props: SceneIndexRefreshSceneButtonProps
) {
  const isRefreshing = useSelector(
    createCommandExecutingSelector(REFRESH_MOVIE)
  );
  const { totalRecords }: AppState['sceneIndex'] = useSelector(
    (state: AppState) => state.sceneIndex
  );

  const dispatch = useDispatch();
  const { isSelectMode, selectedFilterKey } = props;
  const [selectState] = useSelect();
  const { selectedState } = selectState;

  const selectedSceneIds = useMemo(() => {
    return getSelectedIds(selectedState);
  }, [selectedState]);

  // In select mode refresh the selected scenes; otherwise refresh the whole
  // library server-side (REFRESH_MOVIE without ids refreshes everything).
  const scenesToRefresh = useMemo(() => {
    return isSelectMode && selectedSceneIds.length > 0 ? selectedSceneIds : [];
  }, [isSelectMode, selectedSceneIds]);

  const refreshIndexLabel =
    selectedFilterKey === 'all'
      ? translate('UpdateAll')
      : translate('UpdateFiltered');

  const refreshSelectLabel =
    selectedSceneIds.length > 0
      ? translate('UpdateSelected')
      : translate('UpdateAll');

  const onPress = useCallback(() => {
    dispatch(
      executeCommand({
        name: REFRESH_MOVIE,
        movieIds: scenesToRefresh,
      })
    );
  }, [dispatch, scenesToRefresh]);

  return (
    <PageToolbarButton
      label={isSelectMode ? refreshSelectLabel : refreshIndexLabel}
      isSpinning={isRefreshing}
      isDisabled={!totalRecords}
      iconName={icons.REFRESH}
      onPress={onPress}
    />
  );
}

export default SceneIndexRefreshSceneButton;
