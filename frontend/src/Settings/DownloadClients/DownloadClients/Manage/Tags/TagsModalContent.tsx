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

  const allDownloadClients = useSelector(
    (state: AppState) => state.settings.downloadClients
  );

  return (
    <SharedTagsModalContent
      ids={ids}
      items={allDownloadClients.items}
      applyTagsHelpText={translate(
        'ApplyTagsHelpTextHowToApplyDownloadClients'
      )}
      onApplyTagsPress={onApplyTagsPress}
      onModalClose={onModalClose}
    />
  );
}

export default TagsModalContent;
