import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Skeleton } from './Skeleton';

describe('Skeleton', () => {
  it('is hidden from screen readers', () => {
    const { container } = render(<Skeleton height="12px" />);

    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true');
  });

  it('takes the full width unless a width is given', () => {
    const { container } = render(
      <>
        <Skeleton height="12px" />
        <Skeleton width="60%" height="26px" shape="pill" />
      </>,
    );

    const [fullWidth, sized] = Array.from(container.children);
    expect(fullWidth).toHaveStyle({ width: '100%', height: '12px' });
    expect(sized).toHaveStyle({ width: '60%', height: '26px' });
  });
});
