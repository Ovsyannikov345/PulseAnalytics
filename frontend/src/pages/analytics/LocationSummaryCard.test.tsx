import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { MeterLocation } from '../../graphql/generated/enums';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import type {
  AirQualitySummary,
  ByLocation,
  EnergySummary,
  MotionSummary,
} from './locationSummary';
import { LocationSummaryCard } from './LocationSummaryCard';

function ready<T>(data: T, refreshing = false): WidgetQuery<T> {
  return { status: 'ready', data, refreshing };
}

const energy = ready<ByLocation<EnergySummary>>({
  KITCHEN: { average: 512.345, minimum: 12.19, maximum: 989.23 },
});
const airQuality = ready<ByLocation<AirQualitySummary>>({
  KITCHEN: { co2: 712.4, pm25: 22.44, humidity: 38.6 },
});
const motion = ready<ByLocation<MotionSummary>>({
  KITCHEN: { activityRatio: 0.421, samples: 4435 },
});

interface Queries {
  energy?: WidgetQuery<ByLocation<EnergySummary>>;
  airQuality?: WidgetQuery<ByLocation<AirQualitySummary>>;
  motion?: WidgetQuery<ByLocation<MotionSummary>>;
  location?: MeterLocation | null;
}

function renderCard(queries: Queries = {}) {
  return render(
    <LocationSummaryCard
      energy={queries.energy ?? energy}
      airQuality={queries.airQuality ?? airQuality}
      motion={queries.motion ?? motion}
      location={queries.location ?? null}
    />,
  );
}

function rowOf(location: string) {
  const header = screen.getByRole('rowheader', { name: location });
  const row = header.closest('tr');
  if (!row) {
    throw new Error(`No row for ${location}.`);
  }
  return row;
}

describe('LocationSummaryCard', () => {
  it('shows a row per location with the columns from the mockup', () => {
    renderCard();

    const table = screen.getByRole('table', { name: 'Summary by location' });
    expect(
      within(table)
        .getAllByRole('columnheader')
        .map((cell) => cell.textContent),
    ).toEqual([
      'Location',
      'Energy avg',
      'Energy min',
      'Energy max',
      'CO₂ avg, ppm',
      'PM2.5 avg, µg/m³',
      'Humidity avg, %',
      'Motion activity',
      'Samples per meter',
    ]);
    expect(within(table).getAllByRole('rowheader')).toHaveLength(6);
    expect(rowOf('Kitchen')).toHaveTextContent('Kitchen512.3512.19989.2371222.438.642 %4,435');
    expect(rowOf('Garage')).toHaveTextContent('Garage————————');
  });

  it('shows the selected location only', () => {
    renderCard({ location: 'KITCHEN' });

    expect(screen.getAllByRole('rowheader')).toHaveLength(1);
  });

  it('is busy until every operation has answered', () => {
    renderCard({ motion: { status: 'loading' } });

    expect(screen.getByRole('region', { name: 'Summary by location' })).toHaveAttribute(
      'aria-busy',
      'true',
    );
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('shows what loaded and retries the failed operations', async () => {
    const user = userEvent.setup();
    const retryEnergy = vi.fn();
    const retryMotion = vi.fn();
    renderCard({
      energy: { status: 'error', retry: retryEnergy },
      motion: { status: 'error', retry: retryMotion },
    });

    expect(rowOf('Kitchen')).toHaveTextContent('Kitchen———71222.438.6——');
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(screen.getByText("Part of this table couldn't be loaded.")).toBeInTheDocument();
    expect(retryEnergy).toHaveBeenCalledOnce();
    expect(retryMotion).toHaveBeenCalledOnce();
  });

  it('shows one error instead of the table when every operation failed', async () => {
    const user = userEvent.setup();
    const retry = vi.fn();
    renderCard({
      energy: { status: 'error', retry },
      airQuality: { status: 'error', retry },
      motion: { status: 'error', retry },
    });

    expect(screen.getByText("This table couldn't be loaded.")).toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Retry' }));
    expect(retry).toHaveBeenCalledTimes(3);
  });

  it('says so when the range has no samples', () => {
    renderCard({ energy: ready({}), airQuality: ready({}), motion: ready({}) });

    expect(screen.getByRole('heading', { name: 'No samples in this range' })).toBeVisible();
  });

  it('dims the table while newer data loads', () => {
    renderCard({ airQuality: ready(airQuality.status === 'ready' ? airQuality.data : {}, true) });

    expect(screen.getByRole('status')).toHaveTextContent('Updating…');
  });
});
