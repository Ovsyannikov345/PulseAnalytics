import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { MeterLocation } from '../../graphql/generated/enums';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import { EnergyCard } from './EnergyCard';
import { toEnergySeries, type EnergySeries } from './energySeries';

// 15 Nov 14:00–15:00 in Moscow, in 15-minute buckets.
const range = { from: new Date('2026-11-15T11:00:00Z'), to: new Date('2026-11-15T12:00:00Z') };

function row(location: MeterLocation, quarter: number, average: number) {
  return {
    __typename: 'EnergyStatistics' as const,
    location,
    bucketStart: new Date(range.from.getTime() + quarter * 15 * 60_000).toISOString(),
    samples: 60,
    energy: {
      __typename: 'MetricStatistics' as const,
      average,
      minimum: average - 100,
      maximum: average + 100,
    },
  };
}

function series(location: MeterLocation | null, quarters = [0, 1, 2, 3]): EnergySeries {
  return toEnergySeries(
    quarters.flatMap((quarter) => [
      row('KITCHEN', quarter, 400 + quarter),
      row('OFFICE', quarter, 200 + quarter),
    ]),
    range,
    'FIFTEEN_MINUTES',
    location,
  );
}

function ready(data: EnergySeries, refreshing = false): WidgetQuery<EnergySeries> {
  return { status: 'ready', data, refreshing };
}

function renderCard(energy: WidgetQuery<EnergySeries>, location: MeterLocation | null = null) {
  return render(<EnergyCard energy={energy} location={location} bucket="FIFTEEN_MINUTES" />);
}

function legend() {
  return within(screen.getByRole('region', { name: 'Energy' })).getByRole('list');
}

function hoverChart(container: HTMLElement) {
  const surface = container.querySelector('.recharts-wrapper');
  if (!surface) {
    throw new Error('The chart did not render.');
  }
  fireEvent.mouseMove(surface, { clientX: 100, clientY: 100 });
}

describe('EnergyCard', () => {
  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  it('shows a skeleton while the first data loads', () => {
    renderCard({ status: 'loading' });

    expect(screen.getByRole('region', { name: 'Energy' })).toHaveAttribute('aria-busy', 'true');
  });

  it('shows the error with Retry', async () => {
    const user = userEvent.setup();
    const retry = vi.fn();
    renderCard({ status: 'error', retry });

    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(screen.getByText("This chart couldn't be loaded.")).toBeInTheDocument();
    expect(retry).toHaveBeenCalledOnce();
  });

  it('draws a line per location by default', () => {
    const { container } = renderCard(ready(series(null)));

    expect(screen.getByText('Average of each location per 15 minutes')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'By location' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(legend()).toHaveTextContent('KitchenLiving RoomBedroomGarageOfficeCorridor');
    expect(container.querySelectorAll('.recharts-line-curve')).toHaveLength(6);
    expect(
      screen.getByRole('img', {
        name: 'Average energy of each location per 15 minutes from 15 Nov 14:00 to 15 Nov 15:00.',
      }),
    ).toBeInTheDocument();
  });

  it('lists every location in the tooltip, "—" where one has no samples', async () => {
    const { container } = renderCard(ready(series(null)));

    hoverChart(container);

    const tooltip = await screen.findByText(/^15 Nov 14:\d\d–14:\d\d$/);
    expect(tooltip.parentElement).toHaveTextContent(
      /Kitchen40\d\.00Living Room—Bedroom—Garage—Office20\d\.00Corridor—/,
    );
  });

  it('combines the locations into one average line', async () => {
    const user = userEvent.setup();
    const { container } = renderCard(ready(series(null)));

    await user.click(screen.getByRole('button', { name: 'Combined' }));

    expect(screen.getByText('Average of all locations per 15 minutes')).toBeInTheDocument();
    expect(legend()).toHaveTextContent('Average');
    expect(container.querySelectorAll('.recharts-line-curve')).toHaveLength(1);

    hoverChart(container);
    const tooltip = await screen.findByText(/^15 Nov 14:\d\d–14:\d\d$/);
    expect(tooltip.parentElement).toHaveTextContent(
      /Average30\d\.00Min – max10\d\.00 – 50\d\.00Samples120/,
    );
  });

  it("shows one location's line without the view toggle", () => {
    const { container } = renderCard(ready(series('KITCHEN')), 'KITCHEN');

    expect(screen.getByText('Average of Kitchen per 15 minutes')).toBeInTheDocument();
    expect(screen.queryByRole('group', { name: 'Energy view' })).not.toBeInTheDocument();
    expect(legend()).toHaveTextContent('Kitchen');
    expect(container.querySelectorAll('.recharts-line-curve')).toHaveLength(1);
  });

  it('shades and counts the periods without samples', () => {
    const { container } = renderCard(ready(series(null, [0, 3])));

    expect(legend()).toHaveTextContent('No samples');
    expect(container.querySelectorAll('.recharts-reference-area')).toHaveLength(1);
    expect(
      screen.getByRole('img', { name: /15:00\. No samples in 1 period\.$/ }),
    ).toBeInTheDocument();
  });

  it('counts several periods without samples', () => {
    renderCard(ready(toEnergySeries([row('KITCHEN', 1, 400)], range, 'FIFTEEN_MINUTES', null)));

    expect(screen.getByRole('img', { name: /No samples in 2 periods\.$/ })).toBeInTheDocument();
  });

  it('says so when the range has no samples', () => {
    renderCard(ready(series(null, [])));

    expect(screen.getByRole('heading', { name: 'No energy samples in this range' })).toBeVisible();
  });

  it('keeps the chart on screen, dimmed, while newer data loads', () => {
    renderCard(ready(series(null), true));

    expect(screen.getByRole('status')).toHaveTextContent('Updating…');
    expect(screen.getByRole('img', { name: /^Average energy/ })).toBeInTheDocument();
  });
});
