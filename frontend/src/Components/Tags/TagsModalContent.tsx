import { uniq } from 'lodash';
import React, { useCallback, useMemo, useState } from 'react';
import { useSelector } from 'react-redux';
import { Tag } from 'App/State/TagsAppState';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes, kinds, sizes } from 'Helpers/Props';
import createTagsSelector from 'Store/Selectors/createTagsSelector';
import translate from 'Utilities/String/translate';
import styles from './TagsModalContent.css';

export interface TaggedItem {
  id: number;
  tags?: number[];
}

interface TagsModalContentProps {
  // Ids of the selected items whose existing tags should be shown/updated.
  ids: number[];

  // All items of the owning collection (movies, scenes, indexers, ...).
  // Only id and tags are used.
  items: TaggedItem[];

  // Area-specific help text, translated by the caller (e.g.
  // translate('ApplyTagsHelpTextHowToApplyMovies')).
  applyTagsHelpText: string;

  onApplyTagsPress: (tags: number[], applyTags: string) => void;
  onModalClose: () => void;
}

function TagsModalContent(props: TagsModalContentProps) {
  const { ids, items, applyTagsHelpText, onApplyTagsPress, onModalClose } =
    props;

  const tagList: Tag[] = useSelector(createTagsSelector());

  const [tags, setTags] = useState<number[]>([]);
  const [applyTags, setApplyTags] = useState('add');

  // Map/set lookups keep the rendering O(n) instead of the previous O(n^2)
  // `find`/`indexOf` inside `map` pattern.
  const tagById = useMemo(() => {
    const map = new Map<number, Tag>();

    tagList.forEach((tag) => map.set(tag.id, tag));

    return map;
  }, [tagList]);

  const itemTags = useMemo(() => {
    const itemsById = new Map<number, TaggedItem>();

    items.forEach((item) => itemsById.set(item.id, item));

    const acc = ids.reduce((tags: number[], id) => {
      const item = itemsById.get(id);

      if (item) {
        acc.push(...(item.tags ?? []));
      }

      return tags;
    }, []);

    return uniq(acc);
  }, [ids, items]);

  const itemTagSet = useMemo(() => new Set(itemTags), [itemTags]);

  const onTagsChange = useCallback(
    ({ value }: { value: number[] }) => {
      setTags(value);
    },
    [setTags]
  );

  const onApplyTagsChange = useCallback(
    ({ value }: { value: string }) => {
      setApplyTags(value);
    },
    [setApplyTags]
  );

  const onApplyPress = useCallback(() => {
    onApplyTagsPress(tags, applyTags);
  }, [tags, applyTags, onApplyTagsPress]);

  const applyTagsOptions = [
    { key: 'add', value: translate('Add') },
    { key: 'remove', value: translate('Remove') },
    { key: 'replace', value: translate('Replace') },
  ];

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('Tags')}</ModalHeader>

      <ModalBody>
        <Form>
          <FormGroup>
            <FormLabel>{translate('Tags')}</FormLabel>

            <FormInputGroup
              type={inputTypes.TAG}
              name="tags"
              value={tags}
              onChange={onTagsChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('ApplyTags')}</FormLabel>

            <FormInputGroup
              type={inputTypes.SELECT}
              name="applyTags"
              value={applyTags}
              values={applyTagsOptions}
              helpTexts={[
                applyTagsHelpText,
                translate('ApplyTagsHelpTextAdd'),
                translate('ApplyTagsHelpTextRemove'),
                translate('ApplyTagsHelpTextReplace'),
              ]}
              onChange={onApplyTagsChange}
            />
          </FormGroup>

          <FormGroup>
            <FormLabel>{translate('Result')}</FormLabel>

            <div className={styles.result}>
              {itemTags.map((id) => {
                const tag = tagById.get(id);

                if (!tag) {
                  return null;
                }

                const removeTag =
                  (applyTags === 'remove' && tags.indexOf(id) > -1) ||
                  (applyTags === 'replace' && tags.indexOf(id) === -1);

                return (
                  <Label
                    key={tag.id}
                    title={
                      removeTag
                        ? translate('RemovingTag')
                        : translate('ExistingTag')
                    }
                    kind={removeTag ? kinds.INVERSE : kinds.INFO}
                    size={sizes.LARGE}
                  >
                    {tag.label}
                  </Label>
                );
              })}

              {(applyTags === 'add' || applyTags === 'replace') &&
                tags.map((id) => {
                  const tag = tagById.get(id);

                  if (!tag) {
                    return null;
                  }

                  if (itemTagSet.has(id)) {
                    return null;
                  }

                  return (
                    <Label
                      key={tag.id}
                      title={translate('AddingTag')}
                      kind={kinds.SUCCESS}
                      size={sizes.LARGE}
                    >
                      {tag.label}
                    </Label>
                  );
                })}
            </div>
          </FormGroup>
        </Form>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button kind={kinds.PRIMARY} onPress={onApplyPress}>
          {translate('Apply')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default TagsModalContent;
