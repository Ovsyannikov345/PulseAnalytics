import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { MeterLocation } from '../../graphql/generated/enums';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import { AirQualitySection } from './AirQualitySection';
import { toAirQualitySeries, type AirQualitySeries } from './airQualitySeries';

// 15 Nov 14:00–17:00 in Moscow, in hours.
const range = { from: new Date('2026-11-15T11:00:00Z'), to: new Date('2026-11-15T14:00:00Z') };

function metric(average: number) {
  return {
    __typename: 'MetricStatistics' as const,
    average,
    minimum: average / 2,
    maximum: average * 2,
  };
}

function series(hours: number[]): AirQualitySeries {
  return toAirQualitySeries(
    hours.map((hour) => ({
      __typename: 'AirQualityStatistics' as const,
      bucketStart: new Date(range.from.getTime() + hour * 3_600_000).toISOString(),
      samples: 240,
      co2: metric(672),
      pm25: metric(19.25),
      humidity: metric(45.5),
    })),
    range,
    'HOUR',
    null,
  );
}

function renderSection(
  airQuality: WidgetQuery<AirQualitySeries>,
  location: MeterLocation | null = null,
) {
  return render(<AirQualitySection airQuality={airQuality} location={location} />);
}

function metricCard(name: string) {
  return screen.getByRole('region', { name });
}

describe('AirQualitySection', () => {
  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  it('is busy while the first data loads', () => {
    renderSection({ status: 'loading' });

    expect(
      screen.getByRole('region', { name: 'Air quality, all selected locations' }),
    ).toHaveAttribute('aria-busy', 'true');
  });

  it('names the selected location', () => {
    renderSection({ status: 'loading' }, 'OFFICE');

    expect(screen.getByRole('heading', { level: 2, name: 'Air quality, Office' })).toBeVisible();
  });

  it('shows one error with Retry for the three charts', async () => {
    const user = userEvent.setup();
    const retry = vi.fn();
    renderSection({ status: 'error', retry });

    expect(screen.getByText("These charts couldn't be loaded.")).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Retry' }));
    expect(retry).toHaveBeenCalledOnce();
  });

  it('shows a card per metric with its average over the range', () => {
    renderSection({ status: 'ready', data: series([0, 1, 2]), refreshing: false });

    expect(metricCard('CO₂')).toHaveTextContent('avg 672 ppm');
    expect(metricCard('PM2.5')).toHaveTextContent('avg 19.3 µg/m³');
    expect(metricCard('Humidity')).toHaveTextContent('avg 45.5 %');
    expect(screen.getAllByRole('heading', { level: 3 })).toHaveLength(3);
    expect(
      screen.getByRole('img', {
        name: 'CO₂, ppm: average with a minimum to maximum band per 1 hour from 15 Nov 14:00 to 15 Nov 17:00.',
      }),
    ).toBeInTheDocument();
    expect(within(metricCard('CO₂')).getByRole('list')).toHaveTextContent('AverageMin – max');
  });

  it('draws the band from minimum to maximum and shows them in the tooltip', async () => {
    renderSection({ status: 'ready', data: series([0, 1, 2]), refreshing: false });
    const card = metricCard('PM2.5');

    expect(card.querySelector('.recharts-area-area')).toBeInTheDocument();
    const surface = card.querySelector('.recharts-wrapper');
    if (!surface) {
      throw new Error('The chart did not render.');
    }
    fireEvent.mouseMove(surface, { clientX: 100, clientY: 100 });

    const title = await within(card).findByText(/^15 Nov 1\d:00–1\d:00$/);
    expect(title.parentElement).toHaveTextContent(
      'Average19.3 µg/m³Min – max9.6 – 38.5 µg/m³Samples240',
    );
  });

  it('marks the periods without samples', () => {
    renderSection({ status: 'ready', data: series([0, 2]), refreshing: false });

    expect(within(metricCard('Humidity')).getByRole('list')).toHaveTextContent('No samples');
    expect(
      within(metricCard('Humidity')).getByRole('img', { name: /No samples in 1 period\.$/ }),
    ).toBeInTheDocument();
  });

  it('leaves out the average of a metric without samples', () => {
    const data = series([0, 1, 2]);
    const noCo2 = {
      ...data,
      metrics: {
        ...data.metrics,
        co2: {
          points: data.metrics.co2.points.map((point) => ({ ...point, stats: null })),
          average: null,
        },
      },
    };
    renderSection({ status: 'ready', data: noCo2, refreshing: false });

    expect(metricCard('CO₂')).not.toHaveTextContent('avg');
    expect(metricCard('PM2.5')).toHaveTextContent('avg 19.3 µg/m³');
  });

  it('says so when the range has no samples', () => {
    renderSection({ status: 'ready', data: series([]), refreshing: false });

    expect(
      screen.getByRole('heading', { name: 'No air quality samples in this range' }),
    ).toBeVisible();
    expect(screen.queryByRole('region', { name: 'CO₂' })).not.toBeInTheDocument();
  });

  it('dims every card while newer data loads', () => {
    renderSection({ status: 'ready', data: series([0, 1, 2]), refreshing: true });

    expect(screen.getAllByRole('status')).toHaveLength(3);
  });
});
