import type { ButtonHTMLAttributes } from 'react';

import styles from './Button.module.css';
import { cx } from './cx';

export type ButtonVariant = 'primary' | 'secondary' | 'outline';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
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
