import { RefetchDimmer } from '../../components/states/RefetchDimmer';
import { Skeleton } from '../../components/states/Skeleton';
import { WidgetError } from '../../components/states/WidgetError';
import { formatNumber } from '../../domain/formatting';
import { meterTypeLabel } from '../../domain/meters';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import type { MatchCounts as MatchCountsData } from './readingCounts';
import styles from './MatchCounts.module.css';

function Counts({ counts }: Readonly<{ counts: MatchCountsData }>) {
  return (
    <p className={styles.counts}>
      <span>
        <strong className={styles.number}>{formatNumber(counts.total)}</strong>{' '}
        {counts.total === 1 ? 'reading matches' : 'readings match'}
      </span>
      {counts.byType.length > 1
        ? counts.byType.map(({ type, count }) => (
            <span key={type} className={styles.type}>
              {meterTypeLabel(type)}{' '}
              <strong className={styles.number}>{formatNumber(count)}</strong>
            </span>
          ))
        : null}
    </p>
  );
}

/** How many readings the applied filter matches, in all and per selected type. */
export function MatchCounts({ counts }: Readonly<{ counts: WidgetQuery<MatchCountsData> }>) {
  function content() {
    switch (counts.status) {
      case 'loading':
        return <Skeleton width="min(100%, 420px)" height="20px" />;
      case 'error':
        return (
          <WidgetError
            message="The match counts couldn't be loaded."
            onRetry={counts.retry}
            className={styles.error}
          />
        );
      default:
        return (
          <RefetchDimmer refreshing={counts.refreshing}>
            <Counts counts={counts.data} />
          </RefetchDimmer>
        );
    }
  }

  return (
    <div className={styles.matchCounts} aria-busy={counts.status === 'loading'}>
      {content()}
    </div>
  );
}
