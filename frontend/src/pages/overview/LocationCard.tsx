import { Link } from 'react-router';

import { Sparkline } from '../../components/charts/Sparkline';
import { Chip } from '../../components/Chip';
import { Skeleton } from '../../components/states/Skeleton';
import { formatNumber } from '../../domain/formatting';
import { meterLocationLabel } from '../../domain/meters';
import {
  CO2_WARNING_PPM,
  isCo2High,
  isPm25High,
  PM25_WARNING_UG_PER_M3,
} from '../../domain/thresholds';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import type { EnergySparklines } from './energySparklines';
import type { LocationValues } from './latestValues';
import styles from './LocationCard.module.css';

function formatValue(value: number | null, fractionDigits = 0): string {
  return value === null ? '—' : formatNumber(value, fractionDigits);
}

interface MetricProps {
  label: string;
  value: number | null;
  unit: string;
  /** The value that fills the bar. */
  scaleMax: number;
}

function Metric({ label, value, unit, scaleMax }: Readonly<MetricProps>) {
  const share = value === null ? 0 : Math.min(1, Math.max(0, value / scaleMax));

  return (
    <div className={styles.metric}>
      <dt className={styles.metricLabel}>{label}</dt>
      <dd className={styles.metricValue}>
        <span aria-hidden="true" className={styles.bar}>
          <span className={styles.barFill} style={{ width: `${String(share * 100)}%` }} />
        </span>
        <span className={styles.number}>
          {formatValue(value)} <span className={styles.unit}>{unit}</span>
        </span>
      </dd>
    </div>
  );
}

function Trend({
  trend,
  values,
}: Readonly<{ trend: WidgetQuery<EnergySparklines>; values: (number | null)[] | undefined }>) {
  if (trend.status === 'loading') {
    return <Skeleton width="132px" height="36px" />;
  }

  // The trend's error is shown once above the cards; its place stays empty.
  if (trend.status === 'error' || !values) {
    return <span className={styles.trendPlaceholder} />;
  }

  const hasSamples = values.some((value) => value !== null);
  return (
    <Sparkline
      values={values}
      max={trend.data.max}
      label={
        hasSamples ? 'Energy trend over the last 2 hours' : 'No energy samples in the last 2 hours'
      }
    />
  );
}

interface LocationCardProps {
  values: LocationValues;
  trend: WidgetQuery<EnergySparklines>;
}

/** One location's latest values; the whole card links to the location's analytics. */
export function LocationCard({ values, trend }: Readonly<LocationCardProps>) {
  const warnings = [
    values.airQuality && isCo2High(values.airQuality.co2)
      ? `CO₂ above ${formatNumber(CO2_WARNING_PPM)} ppm`
      : null,
    values.airQuality && isPm25High(values.airQuality.pm25)
      ? `PM2.5 above ${formatNumber(PM25_WARNING_UG_PER_M3)} µg/m³`
      : null,
  ].filter((warning) => warning !== null);

  return (
    <article className={styles.card}>
      <div className={styles.top}>
        <h3 className={styles.name}>
          <Link to={`/analytics?location=${values.location}`} className={styles.link}>
            {meterLocationLabel(values.location)}
          </Link>
        </h3>
        {values.motionDetected === true ? <Chip variant="motion">Motion</Chip> : null}
        {values.motionDetected === false ? <Chip variant="neutral">No motion</Chip> : null}
      </div>
      <div className={styles.energyRow}>
        <div className={styles.energy}>
          <span className={styles.energyLabel}>Energy</span>
          <span className={styles.energyValue}>{formatValue(values.energy, 2)}</span>
        </div>
        <div className={styles.trend}>
          <Trend
            trend={trend}
            values={trend.status === 'ready' ? trend.data.values.get(values.location) : undefined}
          />
          <span className={styles.trendCaption}>5-min average, last 2 h</span>
        </div>
      </div>
      <dl className={styles.metrics}>
        <Metric label="CO₂" value={values.airQuality?.co2 ?? null} unit="ppm" scaleMax={2000} />
        <Metric label="PM2.5" value={values.airQuality?.pm25 ?? null} unit="µg/m³" scaleMax={100} />
        <Metric
          label="Humidity"
          value={values.airQuality?.humidity ?? null}
          unit="%"
          scaleMax={100}
        />
      </dl>
      {warnings.length > 0 ? (
        <div className={styles.warnings}>
          {warnings.map((warning) => (
            <Chip key={warning} variant="warning">
              {warning}
            </Chip>
          ))}
        </div>
      ) : null}
    </article>
  );
}

/** Keeps a card's shape while the latest readings load. */
export function LocationCardSkeleton() {
  return (
    <div className={styles.card}>
      <div className={styles.top}>
        <Skeleton width="90px" height="18px" />
        <Skeleton width="70px" height="22px" shape="pill" />
      </div>
      <div className={styles.energyRow}>
        <Skeleton width="110px" height="30px" />
        <Skeleton width="132px" height="36px" />
      </div>
      <div className={styles.skeletonMetrics}>
        <Skeleton height="10px" />
        <Skeleton height="10px" />
        <Skeleton height="10px" />
      </div>
    </div>
  );
}
