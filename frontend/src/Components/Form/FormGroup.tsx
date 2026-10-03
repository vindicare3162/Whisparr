import classNames from 'classnames';
import React, {
  Children,
  ComponentPropsWithoutRef,
  ReactNode,
  useId,
} from 'react';
import { Size } from 'Helpers/Props/sizes';
import FormInputGroup from './FormInputGroup';
import FormLabel from './FormLabel';
import styles from './FormGroup.css';

interface FormGroupProps extends ComponentPropsWithoutRef<'div'> {
  className?: string;
  children: ReactNode;
  size?: Extract<Size, keyof typeof styles>;
  advancedSettings?: boolean;
  isAdvanced?: boolean;
}

function FormGroup(props: FormGroupProps) {
  const {
    className = styles.group,
    children,
    size = 'small',
    advancedSettings = false,
    isAdvanced = false,
    ...otherProps
  } = props;

  // Associates the group's label with its input (label htmlFor <-> input id)
  // so assistive technology announces the label and clicking it focuses the
  // input. Explicit htmlFor / id props on the children win.
  const inputId = useId();

  if (!advancedSettings && isAdvanced) {
    return null;
  }

  const childProps = isAdvanced ? { isAdvanced } : {};

  return (
    <div className={classNames(className, styles[size])} {...otherProps}>
      {Children.map(children, (child) => {
        if (
          !React.isValidElement<{
            htmlFor?: string;
            id?: string;
            isAdvanced?: boolean;
          }>(child)
        ) {
          return child;
        }

        if (child.type === FormLabel) {
          return React.cloneElement(child, { htmlFor: inputId, ...childProps });
        }

        if (child.type === FormInputGroup) {
          return React.cloneElement(child, { id: inputId, ...childProps });
        }

        return React.cloneElement(child, childProps);
      })}
    </div>
  );
}

export default FormGroup;
