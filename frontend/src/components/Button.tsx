import type { ButtonHTMLAttributes } from 'react';

import styles from './Button.module.css';
import { cx } from './cx';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary';
  size?: 'regular' | 'compact';
}

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
