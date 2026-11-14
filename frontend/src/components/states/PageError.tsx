import { Button } from '../Button';
import styles from './PageError.module.css';

interface PageErrorProps {
  /** Reruns every query of the page. */
  onRetry: () => void;
  title?: string;
  description?: string;
}

/** Shown instead of the widgets when all of a page's queries failed, which means the gateway is unreachable. */
export function PageError({
  onRetry,
  title = "Couldn't load the dashboard",
  description = "The API gateway didn't answer. Your filters are kept.",
}: Readonly<PageErrorProps>) {
  return (
    <div role="alert" className={styles.pageError}>
      <svg aria-hidden="true" width="36" height="36" viewBox="0 0 36 36">
        <circle cx="18" cy="18" r="16" className={styles.iconCircle} />
        <path
          d="M18 10v10M18 24.5v1"
          strokeWidth="2.6"
          strokeLinecap="round"
          className={styles.iconMark}
        />
      </svg>
      <h2 className={styles.title}>{title}</h2>
      <p className={styles.description}>{description}</p>
      <Button variant="primary" onClick={onRetry}>
        Retry
      </Button>
    </div>
  );
}
