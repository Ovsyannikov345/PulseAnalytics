import { useId, type ReactNode } from 'react';

import styles from './Card.module.css';
import { cx } from './cx';

interface CardProps {
  title: string;
  description?: string;
  children: ReactNode;
  className?: string;
}

/** A widget's frame: a titled region (`h2`) with an optional description. */
export function Card({ title, description, children, className }: Readonly<CardProps>) {
  const titleId = useId();

  return (
    <section aria-labelledby={titleId} className={cx(styles.card, className)}>
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
