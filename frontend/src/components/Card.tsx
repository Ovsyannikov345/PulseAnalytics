import { useId, type ReactNode } from 'react';

import styles from './Card.module.css';
import { cx } from './cx';
import { RefetchDimmer } from './states/RefetchDimmer';

interface CardProps {
  title: string;
  description?: string;
  /** The first load: the content is a skeleton. */
  busy?: boolean;
  /**
   * Set for cards whose data can reload while it stays on screen: the content dims and the header says
   * "Updating…" while `true`.
   */
  refreshing?: boolean;
  /** Controls at the right of the header, such as a view toggle. */
  actions?: ReactNode;
  headingLevel?: 2 | 3;
  children: ReactNode;
  className?: string;
}

export function Card({
  title,
  description,
  busy = false,
  refreshing,
  actions,
  headingLevel = 2,
  children,
  className,
}: Readonly<CardProps>) {
  const titleId = useId();
  const Heading = headingLevel === 3 ? 'h3' : 'h2';

  return (
    <section aria-labelledby={titleId} aria-busy={busy} className={cx(styles.card, className)}>
      <div className={styles.header}>
        <div className={styles.heading}>
          <Heading id={titleId} className={styles.title}>
            {title}
          </Heading>
          {description ? <p className={styles.description}>{description}</p> : null}
        </div>
        {refreshing || actions ? (
          <div className={styles.aside}>
            {refreshing ? (
              <span role="status" className={styles.updating}>
                <svg aria-hidden="true" width="12" height="12" viewBox="0 0 12 12">
                  <path
                    d="M10.5 6A4.5 4.5 0 1 1 6 1.5"
                    fill="none"
                    strokeWidth="1.6"
                    strokeLinecap="round"
                  />
                </svg>
                Updating…
              </span>
            ) : null}
            {actions}
          </div>
        ) : null}
      </div>
      {refreshing === undefined ? (
        children
      ) : (
        <RefetchDimmer refreshing={refreshing}>
          <div className={styles.content}>{children}</div>
        </RefetchDimmer>
      )}
    </section>
  );
}
