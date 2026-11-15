import { useId, type ReactNode } from 'react';

import styles from './Card.module.css';
import { cx } from './cx';

interface CardProps {
  title: string;
  description?: string;
  busy?: boolean;
  children: ReactNode;
  className?: string;
}

export function Card({
  title,
  description,
  busy = false,
  children,
  className,
}: Readonly<CardProps>) {
  const titleId = useId();

  return (
    <section aria-labelledby={titleId} aria-busy={busy} className={cx(styles.card, className)}>
      <div className={styles.header}>
        <h2 id={titleId} className={styles.title}>
          {title}
        </h2>
        {description ? <p className={styles.description}>{description}</p> : null}
      </div>
      {children}
    </section>
  );
}
