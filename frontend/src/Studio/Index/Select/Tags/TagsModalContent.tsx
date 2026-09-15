import React from 'react';
import { useSelector } from 'react-redux';
import SharedTagsModalContent from 'Components/Tags/TagsModalContent';
import createAllStudiosSelector from 'Store/Selectors/createAllStudiosSelector';
import translate from 'Utilities/String/translate';

interface TagsModalContentProps {
  studioIds: number[];
  onApplyTagsPress: (tags: number[], applyTags: string) => void;
  onModalClose: () => void;
}

function TagsModalContent(props: TagsModalContentProps) {
  const { studioIds, onApplyTagsPress, onModalClose } = props;

  const allStudios = useSelector(createAllStudiosSelector());

  return (
    <SharedTagsModalContent
      ids={studioIds}
      items={allStudios}
      applyTagsHelpText={translate('ApplyTagsHelpTextHowToApplyStudios')}
      onApplyTagsPress={onApplyTagsPress}
      onModalClose={onModalClose}
    />
  );
}

export default TagsModalContent;
