import { useId } from 'react';

import { Button } from '../Button';
import { cx } from '../cx';
import styles from './WidgetError.module.css';

interface WidgetErrorProps {
  /** A fixed client text such as "This chart couldn't be loaded."; server messages are never shown. */
  message: string;
  onRetry: () => void;
  /** Sizes the box, usually to the height of the content it replaces. */
  className?: string;
}

/** Replaces one widget whose query failed; the page's other widgets keep working. */
export function WidgetError({ message, onRetry, className }: Readonly<WidgetErrorProps>) {
  const messageId = useId();

  return (
    <div role="status" className={cx(styles.widgetError, className)}>
      <span id={messageId} className={styles.message}>
        <svg aria-hidden="true" width="16" height="16" viewBox="0 0 16 16" className={styles.icon}>
          <path d="M8 1.5 15 14H1Z" fill="none" strokeWidth="1.4" strokeLinejoin="round" />
          <path d="M8 6v3.5M8 11.5v.3" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
        {message}
      </span>
      {/* Described by the message, so several failed widgets' Retry buttons can be told apart. */}
      <Button size="compact" onClick={onRetry} aria-describedby={messageId}>
        Retry
      </Button>
    </div>
  );
}
