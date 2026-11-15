import { formatPercent } from '../../domain/formatting';
import styles from './CoverageBars.module.css';

export interface CoverageBar {
  key: string;
  share: number;
  title: string;
  axisLabel?: string;
}

interface CoverageBarsProps {
  bars: readonly CoverageBar[];
  label: string;
}

const axisShares = [1, 0.5, 0];

export function CoverageBars({ bars, label }: Readonly<CoverageBarsProps>) {
  const columns = { gridTemplateColumns: `repeat(${String(bars.length)}, minmax(0, 1fr))` };

  return (
    <div className={styles.chart}>
      <div aria-hidden="true" className={styles.yAxis}>
        {axisShares.map((share) => (
          <span key={share}>{formatPercent(share)}</span>
        ))}
      </div>
      <div role="img" aria-label={label} className={styles.plot} style={columns}>
        {bars.map((bar) => (
          <span
            key={bar.key}
            title={bar.title}
            className={styles.bar}
            style={{ height: `${String(Math.min(1, Math.max(0, bar.share)) * 100)}%` }}
          />
        ))}
      </div>
      <div aria-hidden="true" className={styles.xAxis} style={columns}>
        {bars.map((bar) => (
          <span key={bar.key} className={styles.axisLabel}>
            {bar.axisLabel}
          </span>
        ))}
      </div>
    </div>
  );
}
