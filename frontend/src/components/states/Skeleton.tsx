import { cx } from '../cx';
import styles from './Skeleton.module.css';

interface SkeletonProps {
  /** A CSS length; the full width by default. */
  width?: string;
  /** A CSS length. */
  height: string;
  shape?: 'block' | 'pill';
  className?: string;
}

/**
 * A placeholder shape shown while a widget loads for the first time, so the layout doesn't jump.
 * Hidden from screen readers: the widget around it sets `aria-busy` and names what is loading.
 */
export function Skeleton({
  width = '100%',
  height,
  shape = 'block',
  className,
}: Readonly<SkeletonProps>) {
  return (
    <span
      aria-hidden="true"
      className={cx(styles.skeleton, shape === 'pill' && styles.pill, className)}
      style={{ width, height }}
    />
  );
}
