import { WidgetError } from './states/WidgetError';
import { Skeleton } from './states/Skeleton';
import styles from './StatTile.module.css';
import { cx } from './cx';

export type StatTileState =
  | { status: 'loading' }
  | { status: 'ready'; value: string; unit?: string; caption?: string }
  | { status: 'error'; message: string; onRetry: () => void };

type StatTileProps = StatTileState & {
  label: string;
  className?: string;
};

export function StatTile(props: Readonly<StatTileProps>) {
  const { label, className } = props;

  return (
    <div className={cx(styles.tile, className)} aria-busy={props.status === 'loading'}>
      <span className={styles.label}>{label}</span>
      {props.status === 'loading' ? (
        <>
          <Skeleton width="70%" height="32px" />
          <Skeleton width="85%" height="14px" />
        </>
      ) : null}
      {props.status === 'ready' ? (
        <>
          <span className={styles.value}>
            {props.value}
            {props.unit ? <span className={styles.unit}> {props.unit}</span> : null}
          </span>
          {props.caption ? <span className={styles.caption}>{props.caption}</span> : null}
        </>
      ) : null}
      {props.status === 'error' ? (
        <WidgetError message={props.message} onRetry={props.onRetry} className={styles.error} />
      ) : null}
    </div>
  );
}
