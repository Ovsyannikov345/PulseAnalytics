import type { ReactNode } from 'react';

import styles from './RefetchDimmer.module.css';

interface RefetchDimmerProps {
  refreshing: boolean;
  children: ReactNode;
}

/**
 * Keeps the previous data visible while it is refetched: dimmed (styled from `aria-busy`)
 * under a thin indeterminate progress bar.
 */
export function RefetchDimmer({ refreshing, children }: Readonly<RefetchDimmerProps>) {
  return (
    <div className={styles.refetchDimmer} aria-busy={refreshing}>
      {refreshing ? (
        <div role="progressbar" aria-label="Updating" className={styles.progress} />
      ) : null}
      <div className={styles.content}>{children}</div>
    </div>
  );
}
