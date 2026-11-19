import type { ReactNode } from 'react';

import { ValueHighlight } from '../ValueHighlight';
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
  /** Highlights the value briefly when it changes (Live page). */
  highlightChanges?: boolean;
}

export function MetricRow({
  label,
  value,
  unit,
  scaleMax,
  highlightChanges = false,
}: Readonly<MetricRowProps>) {
  const share = value === null ? 0 : Math.min(1, Math.max(0, value / scaleMax));
  const number = (
    <>
      {formatMeterValue(value)} <span className={styles.unit}>{unit}</span>
    </>
  );

  return (
    <div className={styles.metric}>
      <dt className={styles.metricLabel}>{label}</dt>
      <dd className={styles.metricValue}>
        <span aria-hidden="true" className={styles.bar}>
          <span className={styles.barFill} style={{ width: `${String(share * 100)}%` }} />
        </span>
        <span className={styles.number}>
          {highlightChanges ? <ValueHighlight value={value}>{number}</ValueHighlight> : number}
        </span>
      </dd>
    </div>
  );
}
