import styles from './ChartLegend.module.css';

export interface LegendItem {
  label: string;
  /** `line` for a series, `band` for a filled range, `square` for a colour class, `gap` for no samples. */
  swatch: 'line' | 'band' | 'square' | 'gap';
  color?: string;
}

/** The chart's key; the swatches are decoration, the labels carry the meaning. */
export function ChartLegend({ items }: Readonly<{ items: readonly LegendItem[] }>) {
  return (
    <ul className={styles.legend}>
      {items.map((item) => (
        <li key={item.label} className={styles.item}>
          <span
            aria-hidden="true"
            className={styles[item.swatch]}
            style={item.color ? { backgroundColor: item.color } : undefined}
          />
          {item.label}
        </li>
      ))}
    </ul>
  );
}
