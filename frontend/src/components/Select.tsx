import type { SelectHTMLAttributes } from 'react';

import styles from './Control.module.css';
import { cx } from './cx';

export function Select({
  className,
  ...selectProps
}: Readonly<SelectHTMLAttributes<HTMLSelectElement>>) {
  return <select className={cx(styles.control, styles.select, className)} {...selectProps} />;
}
