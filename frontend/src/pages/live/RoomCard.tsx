import { useId } from 'react';

import { Chip } from '../../components/Chip';
import { AirQualityWarnings } from '../../components/meters/AirQualityWarnings';
import { formatMeterValue } from '../../components/meters/formatMeterValue';
import { MetricList, MetricRow } from '../../components/meters/MetricList';
import { Skeleton } from '../../components/states/Skeleton';
import { ValueHighlight } from '../../components/ValueHighlight';
import type { RoomView } from '../../domain/liveReadings';
import { meterLocationLabel } from '../../domain/meters';
import styles from './RoomCard.module.css';

function MotionChip({ motionDetected }: Readonly<{ motionDetected: boolean | null }>) {
  if (motionDetected === null) {
    return <Chip variant="waiting">Motion: waiting</Chip>;
  }

  return (
    <ValueHighlight value={motionDetected} variant="ring">
      {motionDetected ? (
        <Chip variant="motion">Motion</Chip>
      ) : (
        <Chip variant="neutral">No motion</Chip>
      )}
    </ValueHighlight>
  );
}

/** One room's current values; a value that changes is highlighted briefly. */
export function RoomCard({ room }: Readonly<{ room: RoomView }>) {
  const headingId = useId();
  const { airQuality } = room;

  return (
    <article aria-labelledby={headingId} className={styles.card}>
      <div className={styles.top}>
        <h2 id={headingId} className={styles.name}>
          {meterLocationLabel(room.location)}
        </h2>
        <MotionChip motionDetected={room.motionDetected} />
      </div>
      <div className={styles.energy}>
        <span className={styles.energyLabel}>Energy</span>
        {room.energy === null ? (
          <span className={styles.waiting}>Waiting for the first sample</span>
        ) : (
          <ValueHighlight value={room.energy} className={styles.energyValue}>
            {formatMeterValue(room.energy, 2)}
          </ValueHighlight>
        )}
      </div>
      {airQuality ? (
        <MetricList>
          <MetricRow
            label="CO₂"
            value={airQuality.co2}
            unit="ppm"
            scaleMax={2000}
            highlightChanges
          />
          <MetricRow
            label="PM2.5"
            value={airQuality.pm25}
            unit="µg/m³"
            scaleMax={100}
            highlightChanges
          />
          <MetricRow
            label="Humidity"
            value={airQuality.humidity}
            unit="%"
            scaleMax={100}
            highlightChanges
          />
        </MetricList>
      ) : (
        <p className={styles.airQualityWaiting}>Air quality: waiting for the first sample</p>
      )}
      <AirQualityWarnings airQuality={airQuality} />
    </article>
  );
}

export function RoomCardSkeleton() {
  return (
    <div className={styles.card}>
      <div className={styles.top}>
        <Skeleton width="110px" height="18px" />
        <Skeleton width="80px" height="22px" shape="pill" />
      </div>
      <div className={styles.energy}>
        <Skeleton width="50px" height="10px" />
        <Skeleton width="130px" height="30px" />
      </div>
      <div className={styles.skeletonMetrics}>
        <Skeleton height="8px" />
        <Skeleton height="8px" />
        <Skeleton height="8px" />
      </div>
    </div>
  );
}
