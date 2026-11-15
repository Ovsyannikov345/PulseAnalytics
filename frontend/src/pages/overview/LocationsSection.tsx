import { useId } from 'react';

import { EmptyState } from '../../components/states/EmptyState';
import { WidgetError } from '../../components/states/WidgetError';
import { meterLocations } from '../../domain/meters';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import type { EnergySparklines } from './energySparklines';
import type { LatestValues } from './latestValues';
import { LocationCard, LocationCardSkeleton } from './LocationCard';
import styles from './LocationsSection.module.css';

interface LocationsSectionProps {
  latest: WidgetQuery<LatestValues>;
  trend: WidgetQuery<EnergySparklines>;
}

function Content({ latest, trend }: Readonly<LocationsSectionProps>) {
  switch (latest.status) {
    case 'loading':
      return (
        <div className={styles.grid}>
          {meterLocations.map((location) => (
            <LocationCardSkeleton key={location} />
          ))}
        </div>
      );
    case 'error':
      return (
        <WidgetError message="The latest readings couldn't be loaded." onRetry={latest.retry} />
      );
    case 'ready':
      if (latest.data.latestReceivedAt === null) {
        return (
          <EmptyState
            title="No readings stored yet"
            description="The values appear here after the ingestor's first successful poll of WeakApp."
          />
        );
      }

      return (
        <>
          {trend.status === 'error' ? (
            <WidgetError
              message="The energy trends couldn't be loaded."
              onRetry={trend.retry}
              className={styles.trendError}
            />
          ) : null}
          <div className={styles.grid}>
            {latest.data.locations.map((values) => (
              <LocationCard key={values.location} values={values} trend={trend} />
            ))}
          </div>
        </>
      );
  }
}

export function LocationsSection({ latest, trend }: Readonly<LocationsSectionProps>) {
  const headingId = useId();

  return (
    <section
      aria-labelledby={headingId}
      aria-busy={latest.status === 'loading'}
      className={styles.section}
    >
      <div className={styles.header}>
        <h2 id={headingId} className={styles.title}>
          Locations
        </h2>
        {latest.status === 'ready' && latest.data.latestReceivedAt !== null ? (
          <span className={styles.hint}>Select a location to open its analytics</span>
        ) : null}
      </div>
      <Content latest={latest} trend={trend} />
    </section>
  );
}
