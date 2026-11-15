import { Card } from '../../components/Card';
import { EmptyState } from '../../components/states/EmptyState';
import { Skeleton } from '../../components/states/Skeleton';
import { WidgetError } from '../../components/states/WidgetError';
import { formatNumber, formatPercent } from '../../domain/formatting';
import { meterLocationLabel, meterLocations } from '../../domain/meters';
import type { MeterLocation } from '../../graphql/generated/enums';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import {
  toLocationSummary,
  type AirQualitySummary,
  type ByLocation,
  type EnergySummary,
  type LocationSummaryRow,
  type MotionSummary,
} from './locationSummary';
import styles from './LocationSummaryCard.module.css';

const columns = [
  'Energy avg',
  'Energy min',
  'Energy max',
  'CO₂ avg, ppm',
  'PM2.5 avg, µg/m³',
  'Humidity avg, %',
  'Motion activity',
  'Samples per meter',
];

function format(value: number | undefined, fractionDigits = 0): string {
  return value === undefined ? '—' : formatNumber(value, fractionDigits);
}

function cells(row: LocationSummaryRow): string[] {
  const ratio = row.motion?.activityRatio;
  return [
    format(row.energy?.average, 2),
    format(row.energy?.minimum, 2),
    format(row.energy?.maximum, 2),
    format(row.airQuality?.co2),
    format(row.airQuality?.pm25, 1),
    format(row.airQuality?.humidity, 1),
    ratio === undefined ? '—' : formatPercent(ratio),
    // Every poll stores a reading of each meter, so the motion meter's samples stand for all three.
    format(row.motion?.samples),
  ];
}

function readyData<T>(query: WidgetQuery<T>): T | null {
  return query.status === 'ready' ? query.data : null;
}

function SummaryTable({ rows }: Readonly<{ rows: readonly LocationSummaryRow[] }>) {
  if (rows.every((row) => !row.energy && !row.airQuality && !row.motion)) {
    return (
      <EmptyState
        title="No samples in this range"
        description="WeakApp may have been unavailable in this period."
      />
    );
  }

  return (
    <div className={styles.scroll}>
      <table className={styles.table}>
        <caption className="visually-hidden">Summary by location</caption>
        <thead>
          <tr>
            <th scope="col">Location</th>
            {columns.map((column) => (
              <th key={column} scope="col">
                {column}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.location}>
              <th scope="row">{meterLocationLabel(row.location)}</th>
              {cells(row).map((value, index) => (
                <td key={columns[index]}>{value}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

interface LocationSummaryCardProps {
  energy: WidgetQuery<ByLocation<EnergySummary>>;
  airQuality: WidgetQuery<ByLocation<AirQualitySummary>>;
  motion: WidgetQuery<ByLocation<MotionSummary>>;
  /** The applied location filter. */
  location: MeterLocation | null;
}

/** The whole range per location, from three operations; the table shows what loaded. */
export function LocationSummaryCard({
  energy,
  airQuality,
  motion,
  location,
}: Readonly<LocationSummaryCardProps>) {
  const queries = [energy, airQuality, motion];
  const loading = queries.some((query) => query.status === 'loading');
  const retries = queries.flatMap((query) => (query.status === 'error' ? [query.retry] : []));
  const retryFailed = () => {
    for (const retry of retries) {
      retry();
    }
  };

  function content() {
    if (loading) {
      return <Skeleton height="280px" />;
    }
    if (retries.length === queries.length) {
      return <WidgetError message="This table couldn't be loaded." onRetry={retryFailed} />;
    }

    const rows = toLocationSummary(location ? [location] : meterLocations, {
      energy: readyData(energy),
      airQuality: readyData(airQuality),
      motion: readyData(motion),
    });
    return (
      <>
        {retries.length > 0 ? (
          <WidgetError
            message="Part of this table couldn't be loaded."
            onRetry={retryFailed}
            className={styles.partialError}
          />
        ) : null}
        <SummaryTable rows={rows} />
      </>
    );
  }

  return (
    <Card
      title="Summary by location"
      description="The whole range, one row per location"
      busy={loading}
      refreshing={queries.some((query) => query.status === 'ready' && query.refreshing)}
    >
      {content()}
    </Card>
  );
}
