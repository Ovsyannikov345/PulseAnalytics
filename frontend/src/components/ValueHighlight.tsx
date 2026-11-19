import { type ReactNode, useState } from 'react';

import { cx } from './cx';
import styles from './ValueHighlight.module.css';

type HighlightedValue = string | number | boolean | null;

interface ValueHighlightProps {
  /** The value shown inside; `null` means it hasn't arrived yet. */
  value: HighlightedValue;
  /** `background` for numbers, `ring` for a chip. */
  variant?: 'background' | 'ring';
  className?: string;
  children: ReactNode;
}

/**
 * Highlights its content briefly each time `value` changes. The first value to arrive and an equal
 * value again don't count. No animation under reduced motion. Not announced to screen readers.
 */
export function ValueHighlight({
  value,
  variant = 'background',
  className,
  children,
}: Readonly<ValueHighlightProps>) {
  const [tracked, setTracked] = useState({ value, changes: 0 });
  if (!Object.is(tracked.value, value)) {
    setTracked({
      value,
      changes: tracked.value === null ? tracked.changes : tracked.changes + 1,
    });
  }

  const highlighted = tracked.changes > 0;
  return (
    // A new key per change remounts the span, so the fade starts again.
    <span
      key={tracked.changes}
      data-highlighted={highlighted ? 'true' : undefined}
      className={cx(styles[variant], className)}
    >
      {children}
    </span>
  );
}
