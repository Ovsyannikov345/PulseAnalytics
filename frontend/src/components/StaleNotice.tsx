import { formatElapsed, formatTime } from '../domain/formatting';
import styles from './StaleNotice.module.css';

interface StaleNoticeProps {
  latestReceivedAt: Date;
  now: Date;
}

export function StaleNotice({ latestReceivedAt, now }: Readonly<StaleNoticeProps>) {
  return (
    <output className={styles.notice}>
      <svg aria-hidden="true" width="16" height="16" viewBox="0 0 16 16" className={styles.icon}>
        <path
          d="M8 1.5 15 14H1Z"
          strokeWidth="1"
          strokeLinejoin="round"
          className={styles.iconFill}
        />
        <path d="M8 6v3.5M8 11.5v.3" strokeWidth="1.5" strokeLinecap="round" />
      </svg>
      <span>
        No new samples for {formatElapsed(now.getTime() - latestReceivedAt.getTime())}. WeakApp may
        be unavailable; the values shown were received at {formatTime(latestReceivedAt)}.
      </span>
    </output>
  );
}
