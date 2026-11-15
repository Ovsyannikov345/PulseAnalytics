import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { TimeSeriesChart } from './TimeSeriesChart';

const bucketLengthMs = 10;

function renderChart(values: (number | null)[]) {
  const points = values.map((value, index) => ({ time: index * bucketLengthMs, value }));
  return render(
    <TimeSeriesChart
      points={points}
      bucketLengthMs={bucketLengthMs}
      domain={[0, values.length * bucketLengthMs]}
      ticks={[0, 20]}
      formatTick={(time) => `t${String(time)}`}
      formatValue={(value) => `v${String(value)}`}
      renderTooltip={(point) =>
        point.value === null ? 'No samples' : `Value ${String(point.value)}`
      }
      label="Average energy over the last 24 hours"
    />,
  );
}

describe('TimeSeriesChart', () => {
  it('is one image named by its label', () => {
    renderChart([1, 2, 3]);

    expect(
      screen.getByRole('img', { name: 'Average energy over the last 24 hours' }),
    ).toBeInTheDocument();
  });

  it('draws the line and the formatted ticks', () => {
    const { container } = renderChart([1, 2, 3]);

    expect(container.querySelector('.recharts-line-curve')).toHaveAttribute(
      'd',
      expect.stringMatching(/^M/) as string,
    );
    expect(screen.getByText('t0')).toBeInTheDocument();
    expect(screen.getByText('t20')).toBeInTheDocument();
  });

  it('shades each run of buckets without samples', () => {
    const { container } = renderChart([1, null, null, 2, null, 3]);

    expect(container.querySelectorAll('.recharts-reference-area')).toHaveLength(2);
  });

  it('shades nothing without gaps', () => {
    const { container } = renderChart([1, 2, 3]);

    expect(container.querySelectorAll('.recharts-reference-area')).toHaveLength(0);
  });

  it("shows the hovered bucket's tooltip", async () => {
    const { container } = renderChart([1, 2, 3]);
    const surface = container.querySelector('.recharts-wrapper');
    if (!surface) {
      throw new Error('The chart did not render.');
    }

    fireEvent.mouseMove(surface, { clientX: 400, clientY: 100 });

    expect(await screen.findByText(/^Value \d$/)).toBeInTheDocument();
  });
});
