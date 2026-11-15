import type { AnchorHTMLAttributes } from 'react';

import type { ButtonVariant } from './Button';
import styles from './Button.module.css';
import { cx } from './cx';

interface ButtonLinkProps extends AnchorHTMLAttributes<HTMLAnchorElement> {
  href: string;
  variant?: ButtonVariant;
}

/** A link that looks like a `Button`, for actions that navigate or download, such as a file export. */
export function ButtonLink({
  variant = 'secondary',
  className,
  children,
  ...anchorProps
}: Readonly<ButtonLinkProps>) {
  return (
    <a className={cx(styles.button, styles[variant], className)} {...anchorProps}>
      {children}
    </a>
  );
}
