import type { InputHTMLAttributes } from 'react';

import styles from './Control.module.css';
import { cx } from './cx';

export function Input({
  className,
  ...inputProps
}: Readonly<InputHTMLAttributes<HTMLInputElement>>) {
  return <input className={cx(styles.control, className)} {...inputProps} />;
}
