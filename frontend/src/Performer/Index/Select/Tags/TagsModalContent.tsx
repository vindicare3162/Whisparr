import React from 'react';
import { useSelector } from 'react-redux';
import SharedTagsModalContent from 'Components/Tags/TagsModalContent';
import createAllPerformersSelector from 'Store/Selectors/createAllPerformersSelector';
import translate from 'Utilities/String/translate';

interface TagsModalContentProps {
  performerIds: number[];
  onApplyTagsPress: (tags: number[], applyTags: string) => void;
  onModalClose: () => void;
}

function TagsModalContent(props: TagsModalContentProps) {
  const { performerIds, onApplyTagsPress, onModalClose } = props;

  const allPerformers = useSelector(createAllPerformersSelector());

  return (
    <SharedTagsModalContent
      ids={performerIds}
      items={allPerformers}
      applyTagsHelpText={translate('ApplyTagsHelpTextHowToApplyPerformers')}
      onApplyTagsPress={onApplyTagsPress}
      onModalClose={onModalClose}
    />
  );
}

export default TagsModalContent;
