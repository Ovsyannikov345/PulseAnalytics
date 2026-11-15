import type { CSSProperties } from 'react';

import { cx } from '../cx';
import styles from './Heatmap.module.css';

export interface HeatmapCell {
  key: string;
  /** The colour class, from 0 (lightest) to 4 (darkest); `null` for no data. */
  level: number | null;
  title: string;
}

export interface HeatmapRow {
  key: string;
  label: string;
  cells: readonly HeatmapCell[];
}

export interface HeatmapTick {
  /** From 0 (the first column's start) to 1 (the last column's end). */
  position: number;
  label: string;
}

interface HeatmapProps {
  rows: readonly HeatmapRow[];
  ticks: readonly HeatmapTick[];
  label: string;
}

/** Above this many columns cells touch: a 1 px gap per column would leave no room for the colour. */
const MAX_COLUMNS_WITH_GAP = 200;

const levelClasses = [styles.level0, styles.level1, styles.level2, styles.level3, styles.level4];

function tickAlignment(position: number): string | undefined {
  if (position <= 0.02) {
    return styles.tickStart;
  }
  return position >= 0.98 ? styles.tickEnd : undefined;
}

/** Rows of coloured cells on a shared time axis, as a CSS grid; it scrolls sideways inside its box. */
export function Heatmap({ rows, ticks, label }: Readonly<HeatmapProps>) {
  const columnCount = rows[0]?.cells.length ?? 0;
  const columns = {
    gridTemplateColumns: `repeat(${String(columnCount)}, minmax(0, 1fr))`,
    columnGap: columnCount > MAX_COLUMNS_WITH_GAP ? 0 : '1px',
  } satisfies CSSProperties;

  return (
    <div className={styles.scroll}>
      <div className={styles.heatmap}>
        <div role="img" aria-label={label} className={styles.rows}>
          {rows.map((row) => (
            <div key={row.key} className={styles.row}>
              <span className={styles.rowLabel}>{row.label}</span>
              <div className={styles.cells} style={columns}>
                {row.cells.map((cell) => (
                  <span
                    key={cell.key}
                    title={cell.title}
                    className={cx(
                      styles.cell,
                      cell.level === null ? styles.empty : levelClasses[cell.level],
                    )}
                  />
                ))}
              </div>
            </div>
          ))}
        </div>
        <div aria-hidden="true" className={styles.row}>
          <span />
          <div className={styles.axis}>
            {ticks.map((tick) => (
              <span
                key={tick.label}
                className={cx(styles.tick, tickAlignment(tick.position))}
                style={{ left: `${String(tick.position * 100)}%` }}
              >
                {tick.label}
              </span>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
