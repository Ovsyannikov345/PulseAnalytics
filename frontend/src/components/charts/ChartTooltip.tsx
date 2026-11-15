import styles from './ChartTooltip.module.css';

export interface ChartTooltipRow {
  label: string;
  value: string;
  /** A swatch before the label, for charts with several lines. */
  color?: string;
}

interface ChartTooltipProps {
  title: string;
  /** `null` for a bucket without samples. */
  rows: readonly ChartTooltipRow[] | null;
}

/** The content of a chart's tooltip for one bucket: its time span and values. */
export function ChartTooltip({ title, rows }: Readonly<ChartTooltipProps>) {
  return (
    <>
      <p className={styles.title}>{title}</p>
      {rows ? (
        <dl className={styles.rows}>
          {rows.map((row) => (
            <div key={row.label} className={styles.row}>
              <dt className={styles.label}>
                {row.color ? (
                  <span
                    aria-hidden="true"
                    className={styles.swatch}
                    style={{ backgroundColor: row.color }}
                  />
                ) : null}
                {row.label}
              </dt>
              <dd>{row.value}</dd>
            </div>
          ))}
        </dl>
      ) : (
        <p className={styles.empty}>No samples</p>
      )}
    </>
  );
}
