import { Button } from '../../components/Button';
import { formatTime } from '../../domain/formatting';
import styles from './OfflineNotice.module.css';

interface OfflineNoticeProps {
  latestReceivedAt: Date | null;
  onReconnect: () => void;
}

/** Shown when the live connection couldn't be started or closed for good. */
export function OfflineNotice({ latestReceivedAt, onReconnect }: Readonly<OfflineNoticeProps>) {
  return (
    <div role="alert" className={styles.notice}>
      <svg aria-hidden="true" width="36" height="36" viewBox="0 0 36 36" className={styles.icon}>
        <circle cx="18" cy="18" r="16" className={styles.iconFill} />
        <path d="M18 10v10M18 24.5v1" strokeWidth="2.6" strokeLinecap="round" />
      </svg>
      <h2 className={styles.title}>Live updates aren&apos;t available</h2>
      <p className={styles.description}>
        The live updates service didn&apos;t answer. The rooms below show the latest stored values
        {latestReceivedAt ? `, received at ${formatTime(latestReceivedAt)},` : ''} and won&apos;t
        change until you reconnect.
      </p>
      <Button variant="primary" onClick={onReconnect}>
        Reconnect
      </Button>
    </div>
  );
}
