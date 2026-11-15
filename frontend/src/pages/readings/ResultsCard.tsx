import { useId, type ReactNode } from 'react';

import { Button } from '../../components/Button';
import { Card } from '../../components/Card';
import { Select } from '../../components/Select';
import { EmptyState } from '../../components/states/EmptyState';
import { Skeleton } from '../../components/states/Skeleton';
import { WidgetError } from '../../components/states/WidgetError';
import { formatDateTime, formatDateTimeWithSeconds, formatNumber } from '../../domain/formatting';
import { meterLocationLabel, meterTypeLabel } from '../../domain/meters';
import { MeterTypeIcon } from './MeterTypeIcon';
import type { ReadingRow, ReadingsPageData, ReadingValues } from './readingRows';
import styles from './ResultsCard.module.css';
import { pageSizes, type PageSize, type ReadingsTable } from './useReadingsTable';

function Values({ values }: Readonly<{ values: ReadingValues }>) {
  switch (values.kind) {
    case 'airQuality':
      return (
        <>
          CO₂ <strong>{formatNumber(values.co2)}</strong> ppm · PM2.5{' '}
          <strong>{formatNumber(values.pm25)}</strong> µg/m³ · Humidity{' '}
          <strong>{formatNumber(values.humidity)}</strong> %
        </>
      );
    case 'energy':
      return <strong>{formatNumber(values.energy, 2)}</strong>;
    default:
      return (
        <span className={styles.motion}>
          <span
            aria-hidden="true"
            className={values.motionDetected ? styles.motionDot : styles.stillDot}
          />
          {values.motionDetected ? 'Motion detected' : 'No motion'}
        </span>
      );
  }
}

function Row({ row }: Readonly<{ row: ReadingRow }>) {
  return (
    <tr>
      <td className={styles.time}>{formatDateTimeWithSeconds(row.receivedAt)}</td>
      <td>
        <span className={styles.type}>
          <MeterTypeIcon type={row.type} />
          {meterTypeLabel(row.type)}
        </span>
      </td>
      <td>{meterLocationLabel(row.location)}</td>
      <td className={styles.values}>
        <Values values={row.values} />
      </td>
    </tr>
  );
}

function plural(count: number, one: string, many: string): string {
  return count === 1 ? one : many;
}

function shownText(shown: number, total: number | null): ReactNode {
  if (total === null) {
    return (
      <>
        Showing <strong>{formatNumber(shown)}</strong> {plural(shown, 'reading', 'readings')}
      </>
    );
  }

  return (
    <>
      Showing <strong>{formatNumber(shown)}</strong> of <strong>{formatNumber(total)}</strong>{' '}
      {plural(total, 'reading', 'readings')}
    </>
  );
}

interface ResultsCardProps {
  readings: ReadingsTable;
  /** The number of readings the filter matches; `null` while unknown. */
  total: number | null;
  pageSize: PageSize;
  onPageSizeChange: (pageSize: PageSize) => void;
  onReset: () => void;
}

/** The applied filter's readings, newest first, loaded a page at a time. */
export function ResultsCard({
  readings,
  total,
  pageSize,
  onPageSizeChange,
  onReset,
}: Readonly<ResultsCardProps>) {
  const pageSizeId = useId();
  const loadMoreErrorId = useId();
  const { table, loadMore, loadingMore, loadMoreFailed } = readings;

  function footer(data: ReadingsPageData, refreshing: boolean) {
    return (
      <div className={styles.footer}>
        <p className={styles.shown}>{shownText(data.rows.length, total)}</p>
        {data.hasNextPage ? (
          <div className={styles.loadMore}>
            {loadMoreFailed ? (
              <span id={loadMoreErrorId} role="status" className={styles.loadMoreError}>
                More readings couldn&apos;t be loaded.
              </span>
            ) : null}
            <Button
              onClick={loadMore}
              disabled={loadingMore || refreshing}
              aria-describedby={loadMoreFailed ? loadMoreErrorId : undefined}
            >
              {loadingMore ? 'Loading…' : `Load ${String(pageSize)} more`}
            </Button>
          </div>
        ) : null}
      </div>
    );
  }

  function content() {
    switch (table.status) {
      case 'loading':
        return <Skeleton height="420px" />;
      case 'error':
        return <WidgetError message="The readings couldn't be loaded." onRetry={table.retry} />;
      default:
        break;
    }

    const { data } = table;
    if (data.rows.length === 0) {
      return (
        <EmptyState
          title={`No readings between ${formatDateTime(data.range.from)} and ${formatDateTime(data.range.to)}`}
          description="WeakApp may have been unavailable in this period. Try a longer range or other meters."
        >
          <Button onClick={onReset}>Reset filters</Button>
        </EmptyState>
      );
    }

    return (
      <>
        <div className={styles.scroll}>
          <table className={styles.table}>
            <caption className="visually-hidden">Readings, newest first</caption>
            <thead>
              <tr>
                <th scope="col">Received at</th>
                <th scope="col">Type</th>
                <th scope="col">Location</th>
                <th scope="col">Values</th>
              </tr>
            </thead>
            <tbody>
              {data.rows.map((row) => (
                <Row key={row.id} row={row} />
              ))}
            </tbody>
          </table>
        </div>
        {footer(data, table.refreshing)}
      </>
    );
  }

  return (
    <Card
      title="Results"
      busy={table.status === 'loading'}
      refreshing={table.status === 'ready' && table.refreshing}
      actions={
        <div className={styles.pageSize}>
          <label htmlFor={pageSizeId} className={styles.pageSizeLabel}>
            Load at a time
          </label>
          <Select
            id={pageSizeId}
            value={pageSize}
            className={styles.pageSizeSelect}
            onChange={(event) => {
              // The options are the page sizes.
              onPageSizeChange(Number(event.target.value) as PageSize);
            }}
          >
            {pageSizes.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </Select>
        </div>
      }
    >
      {content()}
    </Card>
  );
}
