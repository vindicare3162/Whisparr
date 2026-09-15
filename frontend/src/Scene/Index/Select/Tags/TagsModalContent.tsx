import React from 'react';
import { useSelector } from 'react-redux';
import SharedTagsModalContent from 'Components/Tags/TagsModalContent';
import createAllScenesSelector from 'Store/Selectors/createAllScenesSelector';
import translate from 'Utilities/String/translate';

interface TagsModalContentProps {
  sceneIds: number[];
  onApplyTagsPress: (tags: number[], applyTags: string) => void;
  onModalClose: () => void;
}

function TagsModalContent(props: TagsModalContentProps) {
  const { sceneIds, onApplyTagsPress, onModalClose } = props;

  const allScenes = useSelector(createAllScenesSelector());

  return (
    <SharedTagsModalContent
      ids={sceneIds}
      items={allScenes}
      applyTagsHelpText={translate('ApplyTagsHelpTextHowToApplyScenes')}
      onApplyTagsPress={onApplyTagsPress}
      onModalClose={onModalClose}
    />
  );
}

export default TagsModalContent;
