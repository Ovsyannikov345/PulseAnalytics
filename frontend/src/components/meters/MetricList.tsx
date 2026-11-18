import type { ReactNode } from 'react';

import { formatMeterValue } from './formatMeterValue';
import styles from './MetricList.module.css';

/** Air quality values of a room card: rows with a bar, three columns on a phone. */
export function MetricList({ children }: Readonly<{ children: ReactNode }>) {
  return <dl className={styles.metrics}>{children}</dl>;
}

interface MetricRowProps {
  label: string;
  value: number | null;
  unit: string;
  scaleMax: number;
}

export function MetricRow({ label, value, unit, scaleMax }: Readonly<MetricRowProps>) {
  const share = value === null ? 0 : Math.min(1, Math.max(0, value / scaleMax));

  return (
    <div className={styles.metric}>
      <dt className={styles.metricLabel}>{label}</dt>
      <dd className={styles.metricValue}>
        <span aria-hidden="true" className={styles.bar}>
          <span className={styles.barFill} style={{ width: `${String(share * 100)}%` }} />
        </span>
        <span className={styles.number}>
          {formatMeterValue(value)} <span className={styles.unit}>{unit}</span>
        </span>
      </dd>
    </div>
  );
}
