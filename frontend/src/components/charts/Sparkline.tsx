import styles from './Sparkline.module.css';
import { sparklinePath } from './sparklinePath';

interface SparklineProps {
  values: readonly (number | null)[];
  /** The value at the top; cards that are compared share one. */
  max: number;
  label: string;
  width?: number;
  height?: number;
}

/** A small trend line without axes. */
export function Sparkline({
  values,
  max,
  label,
  width = 132,
  height = 36,
}: Readonly<SparklineProps>) {
  const path = sparklinePath(values, max, width, height);

  return (
    <svg
      role="img"
      aria-label={label}
      width={width}
      height={height}
      viewBox={`0 0 ${String(width)} ${String(height)}`}
      className={styles.sparkline}
    >
      {path ? <path d={path} className={styles.line} /> : null}
    </svg>
  );
}
