import { Link } from 'react-router';

import { Sparkline } from '../../components/charts/Sparkline';
import { Chip } from '../../components/Chip';
import { AirQualityWarnings } from '../../components/meters/AirQualityWarnings';
import { formatMeterValue } from '../../components/meters/formatMeterValue';
import { MetricList, MetricRow } from '../../components/meters/MetricList';
import { Skeleton } from '../../components/states/Skeleton';
import { meterLocationLabel } from '../../domain/meters';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import type { EnergySparklines } from './energySparklines';
import type { LocationValues } from './latestValues';
import styles from './LocationCard.module.css';

function Trend({
  trend,
  values,
}: Readonly<{ trend: WidgetQuery<EnergySparklines>; values: (number | null)[] | undefined }>) {
  if (trend.status === 'loading') {
    return <Skeleton width="132px" height="36px" />;
  }

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

export function LocationCard({ values, trend }: Readonly<LocationCardProps>) {
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
          <span className={styles.energyValue}>{formatMeterValue(values.energy, 2)}</span>
        </div>
        <div className={styles.trend}>
          <Trend
            trend={trend}
            values={trend.status === 'ready' ? trend.data.values.get(values.location) : undefined}
          />
          <span className={styles.trendCaption}>5-min average, last 2 h</span>
        </div>
      </div>
      <MetricList>
        <MetricRow label="CO₂" value={values.airQuality?.co2 ?? null} unit="ppm" scaleMax={2000} />
        <MetricRow
          label="PM2.5"
          value={values.airQuality?.pm25 ?? null}
          unit="µg/m³"
          scaleMax={100}
        />
        <MetricRow
          label="Humidity"
          value={values.airQuality?.humidity ?? null}
          unit="%"
          scaleMax={100}
        />
      </MetricList>
      <AirQualityWarnings airQuality={values.airQuality} />
    </article>
  );
}

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
