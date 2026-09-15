import React from 'react';
import { useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import SharedTagsModalContent from 'Components/Tags/TagsModalContent';
import translate from 'Utilities/String/translate';

interface TagsModalContentProps {
  ids: number[];
  onApplyTagsPress: (tags: number[], applyTags: string) => void;
  onModalClose: () => void;
}

function TagsModalContent(props: TagsModalContentProps) {
  const { ids, onApplyTagsPress, onModalClose } = props;

  const allIndexers = useSelector((state: AppState) => state.settings.indexers);

  return (
    <SharedTagsModalContent
      ids={ids}
      items={allIndexers.items}
      applyTagsHelpText={translate('ApplyTagsHelpTextHowToApplyIndexers')}
      onApplyTagsPress={onApplyTagsPress}
      onModalClose={onModalClose}
    />
  );
}

export default TagsModalContent;
