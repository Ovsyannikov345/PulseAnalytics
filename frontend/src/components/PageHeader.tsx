import type { ReactNode } from 'react';

import styles from './PageHeader.module.css';

interface PageHeaderProps {
  title: string;
  description: string;
  /** Shown below the description, e.g. when the data was received. */
  children?: ReactNode;
}

/** The page's heading and introduction; also sets the document title. */
export function PageHeader({ title, description, children }: Readonly<PageHeaderProps>) {
  return (
    <div className={styles.header}>
      <title>{`${title} · PulseAnalytics`}</title>
      <h1 className={styles.title}>{title}</h1>
      <p className={styles.description}>{description}</p>
      {children}
    </div>
  );
}
