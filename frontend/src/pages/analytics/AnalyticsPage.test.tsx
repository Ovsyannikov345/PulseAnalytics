import type { MockLink } from '@apollo/client/testing';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { INVALID_FILTER_MESSAGE } from '../../graphql/errors';
import { energyMock, rejected, type BucketedVariables } from '../../test/analyticsMocks';
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

type Operation = 'energy';

function renderAnalytics(
  path = '/analytics',
  responses: Partial<Record<Operation, MockLink.MockedResponse[]>> = {},
) {
  const mocks: Record<Operation, MockLink.MockedResponse[]> = { energy: [energy], ...responses };
  return renderRoute(path, { mocks: Object.values(mocks).flat() });
}

function rangeButton(name: string) {
  return within(screen.getByRole('group', { name: 'Time range' })).getByRole('button', { name });
}

describe('AnalyticsPage', () => {
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

      expect(await screen.findByRole('status')).toHaveTextContent('Updating…');
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
      expect(screen.queryByRole('status')).not.toBeInTheDocument();
    });
  });

  describe('errors', () => {
    it('shows the page error when every widget failed, and retries them all', async () => {
      const user = userEvent.setup();
      renderAnalytics('/analytics', { energy: [failing(energy), energy] });

      const alert = await screen.findByRole('alert');
      expect(alert).toHaveTextContent("Couldn't load the dashboard");
      expect(screen.getByRole('region', { name: 'Filters' })).toBeInTheDocument();

      await user.click(within(alert).getByRole('button', { name: 'Retry' }));

      expect(await screen.findByRole('img', { name: /^Average energy/ })).toBeInTheDocument();
    });

    it('shows a rejected filter at the filters instead of the page error', async () => {
      renderAnalytics('/analytics', { energy: [rejected(energy)] });

      expect(await screen.findByRole('alert')).toHaveTextContent(INVALID_FILTER_MESSAGE);
      expect(screen.queryByText("Couldn't load the dashboard")).not.toBeInTheDocument();
      expect(screen.getByText("This chart couldn't be loaded.")).toBeInTheDocument();
    });
  });
});
