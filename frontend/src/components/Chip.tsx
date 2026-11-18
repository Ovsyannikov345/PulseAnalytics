import type { ReactNode } from 'react';

import styles from './Chip.module.css';
import { cx } from './cx';

interface ChipProps {
  /** `waiting`: a dashed outline for a value that hasn't arrived yet. */
  variant: 'motion' | 'neutral' | 'warning' | 'waiting';
  children: ReactNode;
}

export function Chip({ variant, children }: Readonly<ChipProps>) {
  return (
    <span className={cx(styles.chip, styles[variant])}>
      {variant === 'motion' ? (
        <svg aria-hidden="true" width="12" height="12" viewBox="0 0 12 12" className={styles.icon}>
          <circle cx="6" cy="6" r="2.5" className={styles.iconFill} />
          <circle cx="6" cy="6" r="5" fill="none" strokeWidth="1.2" />
        </svg>
      ) : null}
      {variant === 'warning' ? (
        <svg aria-hidden="true" width="14" height="14" viewBox="0 0 14 14" className={styles.icon}>
          <path
            d="M7 1.5 13 12.5H1Z"
            strokeWidth="1"
            strokeLinejoin="round"
            className={styles.iconFill}
          />
          <path d="M7 5.5v3M7 10.2v.3" strokeWidth="1.4" strokeLinecap="round" />
        </svg>
      ) : null}
      {children}
    </span>
  );
}
