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

  const allImportLists = useSelector(
    (state: AppState) => state.settings.importLists
  );

  return (
    <SharedTagsModalContent
      ids={ids}
      items={allImportLists.items}
      applyTagsHelpText={translate('ApplyTagsHelpTextHowToApplyImportLists')}
      onApplyTagsPress={onApplyTagsPress}
      onModalClose={onModalClose}
    />
  );
}

export default TagsModalContent;
