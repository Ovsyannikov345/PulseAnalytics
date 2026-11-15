import { StatTile, type StatTileState } from '../../components/StatTile';
import { formatNumber, formatPercent } from '../../domain/formatting';
import { meterLocationLabel } from '../../domain/meters';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import type { LatestValues } from './latestValues';
import styles from './SummaryTiles.module.css';

type ReadyTile = Extract<StatTileState, { status: 'ready' }>;

const NO_READINGS: ReadyTile = { status: 'ready', value: '—', caption: 'No readings yet' };

/** Tiles from the latest readings: their own widget's error (with Retry) is in the Locations section. */
function latestTile(
  latest: WidgetQuery<LatestValues>,
  toTile: (values: LatestValues) => ReadyTile,
): StatTileState {
  switch (latest.status) {
    case 'loading':
      return { status: 'loading' };
    case 'error':
      return { status: 'ready', value: '—', caption: "Couldn't load" };
    case 'ready':
      return toTile(latest.data);
  }
}

function energyTile({ energyTotal }: LatestValues): ReadyTile {
  return energyTotal === null
    ? NO_READINGS
    : {
        status: 'ready',
        value: formatNumber(energyTotal, 2),
        caption: 'Sum of the latest reading per location',
      };
}

function airQualityTile({ averageAirQuality }: LatestValues): ReadyTile {
  return averageAirQuality === null
    ? NO_READINGS
    : {
        status: 'ready',
        value: formatNumber(averageAirQuality.co2),
        unit: 'ppm',
        caption: `PM2.5 ${formatNumber(averageAirQuality.pm25)} µg/m³ · humidity ${formatNumber(averageAirQuality.humidity)} %`,
      };
}

function motionTile({ motionLocations, motionSensorCount }: LatestValues): ReadyTile {
  return motionSensorCount === 0
    ? NO_READINGS
    : {
        status: 'ready',
        value: String(motionLocations.length),
        unit: `of ${String(motionSensorCount)} rooms`,
        caption:
          motionLocations.length > 0
            ? motionLocations.map(meterLocationLabel).join(', ')
            : 'No motion detected',
      };
}

function samplesTile(sampleCount: WidgetQuery<number>, expectedSamples: number): StatTileState {
  switch (sampleCount.status) {
    case 'loading':
      return { status: 'loading' };
    case 'error':
      return {
        status: 'error',
        message: "The sample count couldn't be loaded.",
        onRetry: sampleCount.retry,
      };
    case 'ready': {
      // Polls aren't spaced exactly 15 s apart, so the count can pass the expected one slightly.
      const share = Math.min(1, sampleCount.data / expectedSamples);
      return {
        status: 'ready',
        value: formatNumber(sampleCount.data),
        caption: `${formatPercent(share)} of expected polls succeeded`,
      };
    }
  }
}

interface SummaryTilesProps {
  latest: WidgetQuery<LatestValues>;
  /** Stored samples in the last 24 hours. */
  sampleCount: WidgetQuery<number>;
  /** The samples the last 24 hours would hold if every poll succeeded. */
  expectedSamples: number;
}

export function SummaryTiles({
  latest,
  sampleCount,
  expectedSamples,
}: Readonly<SummaryTilesProps>) {
  return (
    <section aria-label="Summary" className={styles.tiles}>
      <StatTile label="Energy, all locations" {...latestTile(latest, energyTile)} />
      <StatTile label="Average CO₂" {...latestTile(latest, airQualityTile)} />
      <StatTile label="Motion now" {...latestTile(latest, motionTile)} />
      <StatTile label="Samples, last 24 h" {...samplesTile(sampleCount, expectedSamples)} />
    </section>
  );
}
