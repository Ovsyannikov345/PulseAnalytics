import { useId } from 'react';

import { Button } from '../Button';
import { cx } from '../cx';
import styles from './WidgetError.module.css';

interface WidgetErrorProps {
  message: string;
  onRetry: () => void;
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
      <Button size="compact" onClick={onRetry} aria-describedby={messageId}>
        Retry
      </Button>
    </div>
  );
}
