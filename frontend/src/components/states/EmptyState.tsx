import type { ReactNode } from 'react';

import styles from './EmptyState.module.css';

interface EmptyStateProps {
  title: string;
  description?: string;
  children?: ReactNode;
}

/** Shown when a query succeeded but found nothing for the filters. */
export function EmptyState({ title, description, children }: Readonly<EmptyStateProps>) {
  return (
    <div className={styles.emptyState}>
      <svg aria-hidden="true" width="48" height="48" viewBox="0 0 48 48" className={styles.icon}>
        <rect x="6" y="10" width="36" height="28" rx="5" fill="none" strokeWidth="2" />
        <path
          d="M12 30l7-7 6 5 11-11"
          fill="none"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeDasharray="3 4"
        />
      </svg>
      <h3 className={styles.title}>{title}</h3>
      {description ? <p className={styles.description}>{description}</p> : null}
      {children ? <div className={styles.actions}>{children}</div> : null}
    </div>
  );
}
