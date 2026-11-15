import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CoverageBars } from './CoverageBars';

const bars = [
  {
    key: 'a',
    share: 0.77,
    title: '14:00–15:00 · 1,108 of 1,440 samples (77 %)',
    axisLabel: '14:00',
  },
  { key: 'b', share: 0, title: '15:00–16:00 · No samples' },
  { key: 'c', share: 1.3, title: '16:00–16:30 · more than expected' },
];

describe('CoverageBars', () => {
  it('is one image named by its label, with a bar per entry', () => {
    render(<CoverageBars bars={bars} label="Stored samples per hour" />);

    const chart = screen.getByRole('img', { name: 'Stored samples per hour' });
    expect(chart.children).toHaveLength(3);
    expect(chart).toHaveStyle({ gridTemplateColumns: 'repeat(3, minmax(0, 1fr))' });
  });

  it('sizes the bars by their share, capped at 100 %, with a title each', () => {
    render(<CoverageBars bars={bars} label="Stored samples per hour" />);

    const [first, second, third] = Array.from(
      screen.getByRole('img', { name: 'Stored samples per hour' }).children,
    );
    expect(first).toHaveStyle({ height: '77%' });
    expect(first).toHaveAttribute('title', '14:00–15:00 · 1,108 of 1,440 samples (77 %)');
    expect(second).toHaveStyle({ height: '0%' });
    expect(third).toHaveStyle({ height: '100%' });
  });

  it('shows the axis labels it is given', () => {
    const { container } = render(<CoverageBars bars={bars} label="Stored samples per hour" />);

    expect(container).toHaveTextContent('100 %50 %0 %');
    expect(screen.getByText('14:00')).toBeInTheDocument();
  });
});
