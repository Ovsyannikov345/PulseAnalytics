import type { MockLink } from '@apollo/client/testing';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { INVALID_FILTER_MESSAGE } from '../../graphql/errors';
import {
  airQualityMock,
  airQualitySummaryMock,
  energyMock,
  energySummaryMock,
  motionActivityMock,
  motionSummaryMock,
  rejected,
  type BucketedVariables,
  type StatisticsVariables,
} from '../../test/analyticsMocks';
import { failing } from '../../test/overviewMocks';
import { renderRoute } from '../../test/renderRoute';

// 14:30:00 in Moscow.
const now = new Date('2026-11-15T11:30:00Z');

const lastDay: BucketedVariables = {
  from: '2026-11-14T14:30:00.000+03:00',
  to: '2026-11-15T14:30:00.000+03:00',
  locations: [],
  bucket: 'FIFTEEN_MINUTES',
};

const lastWeek: BucketedVariables = {
  from: '2026-11-08T14:30:00.000+03:00',
  to: '2026-11-15T14:30:00.000+03:00',
  locations: [],
  bucket: 'HOUR',
};

const energy = energyMock(lastDay, [
  { location: 'KITCHEN', bucketStart: '2026-11-15T11:00:00Z', average: 400 },
  { location: 'OFFICE', bucketStart: '2026-11-15T11:00:00Z', average: 200 },
]);

const airQuality = airQualityMock(lastDay, [
  { bucketStart: '2026-11-15T11:00:00Z', co2: 672, pm25: 19, humidity: 45 },
]);

const motion = motionActivityMock(lastDay, [
  { location: 'KITCHEN', bucketStart: '2026-11-15T11:00:00Z', activityRatio: 0.5 },
]);

const lastDayWhole: StatisticsVariables = {
  from: lastDay.from,
  to: lastDay.to,
  locations: [],
};

const energySummary = energySummaryMock(lastDayWhole, [
  { location: 'KITCHEN', average: 512.345, minimum: 12.19, maximum: 989.23 },
]);
const airQualitySummary = airQualitySummaryMock(lastDayWhole, [
  { location: 'KITCHEN', co2: 712, pm25: 22.4, humidity: 38.6 },
]);
const motionSummary = motionSummaryMock(lastDayWhole, [
  { location: 'KITCHEN', activityRatio: 0.42, samples: 4435 },
]);

type Operation =
  'energy' | 'airQuality' | 'motion' | 'energySummary' | 'airQualitySummary' | 'motionSummary';

function renderAnalytics(
  path = '/analytics',
  responses: Partial<Record<Operation, MockLink.MockedResponse[]>> = {},
) {
  const mocks: Record<Operation, MockLink.MockedResponse[]> = {
    energy: [energy],
    airQuality: [airQuality],
    motion: [motion],
    energySummary: [energySummary],
    airQualitySummary: [airQualitySummary],
    motionSummary: [motionSummary],
    ...responses,
  };
  return renderRoute(path, { mocks: Object.values(mocks).flat() });
}

function rangeButton(name: string) {
  return within(screen.getByRole('group', { name: 'Time range' })).getByRole('button', { name });
}

// Each test renders the whole page: five Recharts charts and a table, slow under coverage on a busy machine.
describe('AnalyticsPage', { timeout: 15_000 }, () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(now);
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('energy', () => {
    it('loads the last 24 hours by location in 15-minute buckets', async () => {
      renderAnalytics();

      expect(screen.getByRole('region', { name: 'Energy' })).toHaveAttribute('aria-busy', 'true');
      expect(
        await screen.findByRole('img', {
          name: /^Average energy of each location per 15 minutes/,
        }),
      ).toBeInTheDocument();
    });

    it('asks for the selected location only', async () => {
      renderAnalytics('/analytics?location=GARAGE', {
        energy: [
          energyMock({ ...lastDay, locations: ['GARAGE'] }, [
            { location: 'GARAGE', bucketStart: '2026-11-15T11:00:00Z', average: 900 },
          ]),
        ],
      });

      expect(
        await screen.findByRole('img', { name: /^Average energy of Garage per 15 minutes/ }),
      ).toBeInTheDocument();
    });

    it('keeps the previous chart, dimmed, while a new range loads', async () => {
      const user = userEvent.setup();
      // The week never arrives, so the test can't race it.
      renderAnalytics('/analytics', {
        energy: [energy, { ...energyMock(lastWeek, []), delay: Infinity }],
      });
      await screen.findByRole('img', { name: /^Average energy/ });

      await user.click(rangeButton('7 d'));

      const card = screen.getByRole('region', { name: 'Energy' });
      expect(await within(card).findByRole('status')).toHaveTextContent('Updating…');
      // Still the 24-hour series, on the grid it was loaded for.
      expect(
        screen.getByRole('img', { name: /per 15 minutes from 14 Nov 14:30 to 15 Nov 14:30\./ }),
      ).toBeInTheDocument();
    });

    it('shows a new range once it arrives', async () => {
      const user = userEvent.setup();
      renderAnalytics('/analytics', {
        energy: [
          energy,
          energyMock(lastWeek, [
            { location: 'KITCHEN', bucketStart: '2026-11-15T10:00:00Z', average: 500 },
          ]),
        ],
      });
      await screen.findByRole('img', { name: /^Average energy/ });

      await user.click(rangeButton('7 d'));

      expect(
        await screen.findByRole('img', { name: /per 1 hour from 8 Nov 14:30 to 15 Nov 14:30\./ }),
      ).toBeInTheDocument();
      expect(
        within(screen.getByRole('region', { name: 'Energy' })).queryByRole('status'),
      ).not.toBeInTheDocument();
    });
  });

  describe('air quality', () => {
    it('loads the last 24 hours of all locations in 15-minute buckets', async () => {
      renderAnalytics();

      const section = screen.getByRole('region', { name: 'Air quality, all selected locations' });
      expect(section).toHaveAttribute('aria-busy', 'true');
      expect(await screen.findByRole('region', { name: 'CO₂' })).toHaveTextContent('avg 672 ppm');
    });

    it('asks for the selected location only', async () => {
      renderAnalytics('/analytics?location=OFFICE', {
        energy: [energyMock({ ...lastDay, locations: ['OFFICE'] }, [])],
        airQuality: [
          airQualityMock({ ...lastDay, locations: ['OFFICE'] }, [
            { bucketStart: '2026-11-15T11:00:00Z', co2: 900, pm25: 40, humidity: 30 },
          ]),
        ],
      });

      expect(await screen.findByRole('region', { name: 'CO₂' })).toHaveTextContent('avg 900 ppm');
      expect(screen.getByRole('heading', { level: 2, name: 'Air quality, Office' })).toBeVisible();
    });

    it('fails on its own while the energy chart works', async () => {
      renderAnalytics('/analytics', { airQuality: [failing(airQuality)] });

      expect(await screen.findByText("These charts couldn't be loaded.")).toBeInTheDocument();
      expect(await screen.findByRole('img', { name: /^Average energy/ })).toBeInTheDocument();
      expect(screen.queryByText("Couldn't load the dashboard")).not.toBeInTheDocument();
    });
  });

  describe('motion activity', () => {
    it('loads every location of the last 24 hours in 15-minute buckets', async () => {
      renderAnalytics();

      expect(screen.getByRole('region', { name: 'Motion activity' })).toHaveAttribute(
        'aria-busy',
        'true',
      );
      expect(
        await screen.findByRole('img', {
          name: /Over the whole range: Kitchen 50 %, Living Room no samples/,
        }),
      ).toBeInTheDocument();
    });

    it('shows a row for the selected location only', async () => {
      renderAnalytics('/analytics?location=KITCHEN', {
        energy: [energyMock({ ...lastDay, locations: ['KITCHEN'] }, [])],
        airQuality: [airQualityMock({ ...lastDay, locations: ['KITCHEN'] }, [])],
        motion: [
          motionActivityMock({ ...lastDay, locations: ['KITCHEN'] }, [
            { location: 'KITCHEN', bucketStart: '2026-11-15T11:00:00Z', activityRatio: 1 },
          ]),
        ],
      });

      expect(
        await screen.findByRole('img', { name: /Over the whole range: Kitchen 100 %.$/ }),
      ).toBeInTheDocument();
    });
  });

  describe('summary by location', () => {
    it('joins the three operations into one row per location', async () => {
      renderAnalytics();

      const kitchen = await screen.findByRole('rowheader', { name: 'Kitchen' });
      expect(kitchen.closest('tr')).toHaveTextContent(
        'Kitchen512.3512.19989.2371222.438.642 %4,435',
      );
      expect(screen.getAllByRole('rowheader')).toHaveLength(6);
    });

    it('asks for the selected location only', async () => {
      const kitchenOnly = { ...lastDayWhole, locations: ['KITCHEN' as const] };
      renderAnalytics('/analytics?location=KITCHEN', {
        energy: [energyMock({ ...lastDay, locations: ['KITCHEN'] }, [])],
        airQuality: [airQualityMock({ ...lastDay, locations: ['KITCHEN'] }, [])],
        motion: [motionActivityMock({ ...lastDay, locations: ['KITCHEN'] }, [])],
        energySummary: [energySummaryMock(kitchenOnly, [])],
        airQualitySummary: [airQualitySummaryMock(kitchenOnly, [])],
        motionSummary: [
          motionSummaryMock(kitchenOnly, [{ location: 'KITCHEN', activityRatio: 1, samples: 10 }]),
        ],
      });

      const kitchen = await screen.findByRole('rowheader', { name: 'Kitchen' });
      expect(kitchen.closest('tr')).toHaveTextContent('Kitchen——————100 %10');
      expect(screen.getAllByRole('rowheader')).toHaveLength(1);
    });
  });

  describe('errors', () => {
    it('shows the page error when every widget failed, and retries them all', async () => {
      const user = userEvent.setup();
      renderAnalytics('/analytics', {
        energy: [failing(energy), energy],
        airQuality: [failing(airQuality), airQuality],
        motion: [failing(motion), motion],
        energySummary: [failing(energySummary), energySummary],
        airQualitySummary: [failing(airQualitySummary), airQualitySummary],
        motionSummary: [failing(motionSummary), motionSummary],
      });

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent("Couldn't load the dashboard");
      expect(screen.getByRole('region', { name: 'Filters' })).toBeInTheDocument();

      await user.click(within(alert).getByRole('button', { name: 'Retry' }));

      expect(await screen.findByRole('img', { name: /^Average energy/ })).toBeInTheDocument();
      expect(await screen.findByRole('region', { name: 'CO₂' })).toBeInTheDocument();
      expect(await screen.findByRole('table', { name: 'Summary by location' })).toBeInTheDocument();
    });

    it('shows a rejected filter at the filters instead of the page error', async () => {
      renderAnalytics('/analytics', {
        energy: [rejected(energy)],
        airQuality: [rejected(airQuality)],
        motion: [rejected(motion)],
        energySummary: [rejected(energySummary)],
        airQualitySummary: [rejected(airQualitySummary)],
        motionSummary: [rejected(motionSummary)],
      });

      await waitFor(() => {
        expect(screen.getAllByText("This chart couldn't be loaded.")).toHaveLength(2);
      });
      expect(await screen.findByText("These charts couldn't be loaded.")).toBeInTheDocument();
      expect(await screen.findByText("This table couldn't be loaded.")).toBeInTheDocument();
      expect(screen.getByRole('alert')).toHaveTextContent(INVALID_FILTER_MESSAGE);
      expect(screen.queryByText("Couldn't load the dashboard")).not.toBeInTheDocument();
    });
  });
});
