import React from 'react';
import { useSelector } from 'react-redux';
import SharedTagsModalContent from 'Components/Tags/TagsModalContent';
import createAllMoviesSelector from 'Store/Selectors/createAllMoviesSelector';
import translate from 'Utilities/String/translate';

interface TagsModalContentProps {
  movieIds: number[];
  onApplyTagsPress: (tags: number[], applyTags: string) => void;
  onModalClose: () => void;
}

function TagsModalContent(props: TagsModalContentProps) {
  const { movieIds, onApplyTagsPress, onModalClose } = props;

  const allMovies = useSelector(createAllMoviesSelector());

  return (
    <SharedTagsModalContent
      ids={movieIds}
      items={allMovies}
      applyTagsHelpText={translate('ApplyTagsHelpTextHowToApplyMovies')}
      onApplyTagsPress={onApplyTagsPress}
      onModalClose={onModalClose}
    />
  );
}

export default TagsModalContent;
