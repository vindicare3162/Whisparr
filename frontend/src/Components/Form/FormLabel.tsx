import classNames from 'classnames';
import React, { ReactNode } from 'react';
import { Size } from 'Helpers/Props/sizes';
import styles from './FormLabel.css';

interface FormLabelProps {
  children: ReactNode;
  className?: string;
  errorClassName?: string;
  size?: Extract<Size, keyof typeof styles>;
  name?: string;
  htmlFor?: string;
  hasError?: boolean;
  isAdvanced?: boolean;
}

function FormLabel(props: FormLabelProps) {
  const {
    children,
    className = styles.label,
    errorClassName = styles.hasError,
    size = 'large',
    name,
    htmlFor = name,
    hasError,
    isAdvanced = false,
  } = props;

  return (
    <label
      className={classNames(
        className,
        styles[size],
        hasError && errorClassName,
        isAdvanced && styles.isAdvanced
      )}
      htmlFor={htmlFor}
    >
      {children}
    </label>
  );
}

export default FormLabel;
