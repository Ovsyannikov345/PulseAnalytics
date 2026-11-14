import type { ButtonHTMLAttributes } from 'react';

import styles from './Button.module.css';
import { cx } from './cx';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary';
  /** Compact buttons are narrower and use smaller text, but stay 44 px tall like all touch targets. */
  size?: 'regular' | 'compact';
}

/** A button in the mockups' style; `type` defaults to "button", so it never submits a form by accident. */
export function Button({
  variant = 'secondary',
  size = 'regular',
  type = 'button',
  className,
  ...buttonProps
}: Readonly<ButtonProps>) {
  return (
    <button
      type={type}
      className={cx(
        styles.button,
        styles[variant],
        size === 'compact' && styles.compact,
        className,
      )}
      {...buttonProps}
    />
  );
}
