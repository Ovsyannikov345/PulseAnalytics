import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Heatmap, type HeatmapRow } from './Heatmap';

function rowOf(key: string, levels: (number | null)[]): HeatmapRow {
  return {
    key,
    label: key,
    cells: levels.map((level, index) => ({
      key: String(index),
      level,
      title: `${key} ${String(index)}`,
    })),
  };
}

describe('Heatmap', () => {
  it('is one image named by its label, with a titled cell per value', () => {
    const { container } = render(
      <Heatmap
        rows={[rowOf('Kitchen', [0, 4, null]), rowOf('Office', [2, 1, 3])]}
        ticks={[]}
        label="Motion activity"
      />,
    );

    expect(screen.getByRole('img', { name: 'Motion activity' })).toHaveTextContent('KitchenOffice');
    expect(container.querySelectorAll('[title]')).toHaveLength(6);
    expect(screen.getByTitle('Kitchen 2').className).toMatch(/empty/);
    expect(screen.getByTitle('Kitchen 1').className).toMatch(/level4/);
  });

  it('separates the columns of short ranges only', () => {
    const { container, rerender } = render(
      <Heatmap rows={[rowOf('Kitchen', [0, 1])]} ticks={[]} label="Motion activity" />,
    );
    const cells = () => container.querySelector('[title]')?.parentElement;

    expect(cells()).toHaveStyle({ columnGap: '1px' });

    rerender(
      <Heatmap
        rows={[
          rowOf(
            'Kitchen',
            Array.from({ length: 201 }, () => 0),
          ),
        ]}
        ticks={[]}
        label="Motion activity"
      />,
    );
    expect(cells()).toHaveStyle({ columnGap: '0' });
  });

  it('places the axis labels by position and keeps the outer ones inside', () => {
    render(
      <Heatmap
        rows={[]}
        ticks={[
          { position: 0, label: '14:30' },
          { position: 0.5, label: '02:30' },
          { position: 1, label: '14:31' },
        ]}
        label="Motion activity"
      />,
    );

    expect(screen.getByText('14:30')).toHaveStyle({ left: '0%' });
    expect(screen.getByText('14:30').className).toMatch(/tickStart/);
    expect(screen.getByText('02:30')).toHaveStyle({ left: '50%' });
    expect(screen.getByText('02:30').className).not.toMatch(/tickStart|tickEnd/);
    expect(screen.getByText('14:31')).toHaveStyle({ left: '100%' });
    expect(screen.getByText('14:31').className).toMatch(/tickEnd/);
  });
});
