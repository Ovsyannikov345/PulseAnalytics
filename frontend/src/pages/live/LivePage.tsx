import { skipToken, useQuery } from '@apollo/client/react';
import { useMemo } from 'react';
import { Link } from 'react-router';

import { cx } from '../../components/cx';
import { PageHeader } from '../../components/PageHeader';
import { StaleNotice } from '../../components/StaleNotice';
import { Skeleton } from '../../components/states/Skeleton';
import { WidgetError } from '../../components/states/WidgetError';
import { POLL_PERIOD_MS } from '../../domain/expectedSamples';
import { formatTime } from '../../domain/formatting';
import { isStale } from '../../domain/freshness';
import {
  mergeReadings,
  newestReceivedAt,
  noLiveReadings,
  toRoomViews,
} from '../../domain/liveReadings';
import { meterLocations } from '../../domain/meters';
import { LiveLatestReadingsDocument } from '../../graphql/generated/graphql';
import { useLiveReadings } from '../../realtime/useLiveReadings';
import { ConnectionStatus } from './ConnectionStatus';
import { toLiveReadings } from './latestReadings';
import styles from './LivePage.module.css';
import { OfflineNotice } from './OfflineNotice';
import { RoomCard, RoomCardSkeleton } from './RoomCard';
import { useNow } from './useNow';

/**
 * The newest sample of every meter: the stored values from `latestReadings`, then each reading the
 * readings hub pushes once it is stored, merged per meter by the newest `receivedAt`.
 */
export function LivePage() {
  const live = useLiveReadings({
    // Pushes sent while the connection was down were missed; the stored values fill the gap.
    onReconnected: () => {
      refetchLatest();
    },
  });
  // Started once the hub's first start has settled, so no reading stored in between is missed.
  const latestResult = useQuery(
    LiveLatestReadingsDocument,
    live.settled ? { fetchPolicy: 'no-cache' } : skipToken,
  );
  const refetchLatest = () => {
    latestResult.refetch().catch(() => {
      // Shown by the stored values' error.
    });
  };
  const now = useNow();

  const latestRows = (latestResult.data ?? latestResult.previousData)?.latestReadings;
  const storedReadings = useMemo(
    () => (latestRows ? toLiveReadings(latestRows) : noLiveReadings),
    [latestRows],
  );
  const readings = useMemo(
    () => mergeReadings(storedReadings, live.readings.values()),
    [storedReadings, live.readings],
  );
  const rooms = useMemo(() => toRoomViews(readings), [readings]);
  const latestReceivedAt = newestReceivedAt(readings);

  const waitingForValues = readings.size === 0 && (!live.settled || latestResult.loading);
  const storedValuesFailed = latestResult.error !== undefined && !latestResult.loading;

  return (
    <>
      <div className={styles.header}>
        <PageHeader
          title="Live"
          description={`The newest sample of every meter, updated as soon as it is stored. Meters are polled about every ${String(POLL_PERIOD_MS / 1000)} s.`}
        />
        <div className={styles.status}>
          <ConnectionStatus status={live.status} />
          {waitingForValues ? <Skeleton width="220px" height="18px" /> : null}
          {latestReceivedAt ? (
            <span className={styles.meta}>
              {live.status === 'reconnecting'
                ? 'Values received up to'
                : 'Latest samples received at'}{' '}
              {formatTime(latestReceivedAt)}
            </span>
          ) : null}
        </div>
      </div>

      {live.status === 'offline' ? (
        <OfflineNotice latestReceivedAt={latestReceivedAt} onReconnect={live.reconnect} />
      ) : null}
      {live.status === 'live' && latestReceivedAt && isStale(latestReceivedAt, now) ? (
        <StaleNotice latestReceivedAt={latestReceivedAt} now={now} />
      ) : null}
      {storedValuesFailed ? (
        <WidgetError
          message="Couldn't load the latest stored values. Rooms fill in as new samples arrive."
          onRetry={refetchLatest}
        />
      ) : null}

      {waitingForValues ? (
        <div aria-busy="true" aria-label="Loading the latest values" className={styles.rooms}>
          {meterLocations.map((location) => (
            <RoomCardSkeleton key={location} />
          ))}
        </div>
      ) : (
        <ul
          aria-label="Rooms"
          className={cx(styles.rooms, live.status === 'reconnecting' && styles.dimmed)}
        >
          {rooms.map((room) => (
            <li key={room.location} className={styles.room}>
              <RoomCard room={room} />
            </li>
          ))}
        </ul>
      )}

      <footer className={styles.footer}>
        <span>
          Every poll is a new sample, so a value can arrive again unchanged. Values that changed are
          highlighted briefly.
        </span>
        <Link to="/">Trends and coverage are on the Overview</Link>
      </footer>
    </>
  );
}
