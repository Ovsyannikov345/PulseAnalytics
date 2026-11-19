import { cx } from '../../components/cx';
import type { HubStatus } from '../../realtime/useLiveReadings';
import styles from './ConnectionStatus.module.css';

const labels: Record<HubStatus, string> = {
  connecting: 'Connecting…',
  live: 'Live',
  reconnecting: 'Reconnecting…',
  offline: 'Offline',
};

function StatusIcon({ status }: Readonly<{ status: HubStatus }>) {
  switch (status) {
    case 'live':
      return <span aria-hidden="true" className={styles.liveDot} />;
    case 'offline':
      return (
        <svg aria-hidden="true" width="12" height="12" viewBox="0 0 12 12" className={styles.icon}>
          <path d="M2 2l8 8M10 2l-8 8" strokeWidth="1.6" strokeLinecap="round" />
        </svg>
      );
    case 'connecting':
    case 'reconnecting':
      return (
        <svg aria-hidden="true" width="12" height="12" viewBox="0 0 12 12" className={styles.icon}>
          <path
            d="M10.5 6A4.5 4.5 0 1 1 6 1.5"
            fill="none"
            strokeWidth="1.6"
            strokeLinecap="round"
          />
        </svg>
      );
  }
}

/** The state of the live connection; the only part of the page announced to screen readers. */
export function ConnectionStatus({ status }: Readonly<{ status: HubStatus }>) {
  return (
    <output className={cx(styles.status, styles[status])}>
      <StatusIcon status={status} />
      {labels[status]}
    </output>
  );
}
