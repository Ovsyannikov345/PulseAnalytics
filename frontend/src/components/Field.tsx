import { useId, type ReactNode } from 'react';

import { cx } from './cx';
import styles from './Field.module.css';

/** What the field's control needs to be named and described by the field. */
export interface FieldControlProps {
  id: string;
  'aria-describedby'?: string;
  'aria-invalid'?: true;
}

interface FieldProps {
  label: string;
  hint?: string | null;
  error?: string | null;
  className?: string;
  children: (control: FieldControlProps) => ReactNode;
}

/** A label above its control, with an optional hint and an error message below it. */
export function Field({ label, hint, error, className, children }: Readonly<FieldProps>) {
  const id = useId();
  const hintId = `${id}-hint`;
  const errorId = `${id}-error`;
  const describedBy = [error ? errorId : null, hint ? hintId : null].filter(
    (part) => part !== null,
  );

  return (
    <div className={cx(styles.field, className)}>
      <label htmlFor={id} className={styles.label}>
        {label}
      </label>
      {children({
        id,
        ...(describedBy.length > 0 ? { 'aria-describedby': describedBy.join(' ') } : {}),
        ...(error ? { 'aria-invalid': true } : {}),
      })}
      {error ? (
        <span id={errorId} className={styles.error}>
          <svg
            aria-hidden="true"
            width="14"
            height="14"
            viewBox="0 0 14 14"
            className={styles.icon}
          >
            <circle cx="7" cy="7" r="6" className={styles.iconCircle} />
            <path d="M7 3.8v3.6M7 9.6v.2" strokeWidth="1.5" strokeLinecap="round" />
          </svg>
          {error}
        </span>
      ) : null}
      {hint ? (
        <span id={hintId} className={styles.hint}>
          {hint}
        </span>
      ) : null}
    </div>
  );
}
