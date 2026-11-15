import type { MockLink } from '@apollo/client/testing';
import { fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { latestReadings } from '../../test/overviewFixtures';
import {
  energySparklinesMock,
  energyTrendMock,
  failing,
  ingestionCoverageMock,
  latestReadingsMock,
  sampleCountMock,
} from '../../test/overviewMocks';
import { renderRoute } from '../../test/renderRoute';

// 14:30:00 in Moscow.
const now = new Date('2026-11-15T11:30:00Z');
const fresh = '2026-11-15T11:29:47Z';

const lastDay = { from: '2026-11-14T14:30:00.000+03:00', to: '2026-11-15T14:30:00.000+03:00' };
const lastTwoHours = { from: '2026-11-15T12:30:00.000+03:00', to: '2026-11-15T14:30:00.000+03:00' };

const HOUR_MS = 3_600_000;

function isoAt(epochMs: number): string {
  return new Date(epochMs).toISOString();
}

const latest = latestReadingsMock(latestReadings(fresh));

// 77 % of the 103,680 expected samples.
const sampleCount = sampleCountMock(lastDay, 79_836);

const sparklines = energySparklinesMock(lastTwoHours, [
  { location: 'KITCHEN', bucketStart: '2026-11-15T09:30:00Z', average: 400 },
  { location: 'KITCHEN', bucketStart: '2026-11-15T09:35:00Z', average: 500 },
]);

// 96 quarter hours from 11:30Z the day before, averages 400…495, with no samples in the 59th and 60th.
const energyTrend = energyTrendMock(
  lastDay,
  Array.from({ length: 96 }, (_, index) => index)
    .filter((index) => index !== 58 && index !== 59)
    .map((index) => ({
      bucketStart: isoAt(Date.parse('2026-11-14T11:30:00Z') + index * 15 * 60_000),
      average: 400 + index,
      minimum: 12.5,
      maximum: 989.25,
      samples: 69,
    })),
);

// 25 hours from 11:00Z the day before: half hours at both ends (720 samples expected), full hours
// otherwise (1,440). 77 % stored, except 41 % in 05:00–06:00 local (02:00Z).
const coverage = ingestionCoverageMock(
  lastDay,
  Array.from({ length: 25 }, (_, index) => {
    const start = Date.parse('2026-11-14T11:00:00Z') + index * HOUR_MS;
    const partial = index === 0 || index === 24;
    const low = start === Date.parse('2026-11-15T02:00:00Z');
    return { bucketStart: isoAt(start), samples: partial ? 554 : low ? 590 : 1109 };
  }),
);

type Operation = 'latest' | 'sampleCount' | 'sparklines' | 'energyTrend' | 'coverage';

function renderOverview(responses: Partial<Record<Operation, MockLink.MockedResponse[]>> = {}) {
  const mocks: Record<Operation, MockLink.MockedResponse[]> = {
    latest: [latest],
    sampleCount: [sampleCount],
    sparklines: [sparklines],
    energyTrend: [energyTrend],
    coverage: [coverage],
    ...responses,
  };
  return renderRoute('/', { mocks: Object.values(mocks).flat() });
}

function found<T>(element: T | null, description: string): T {
  if (element === null) {
    throw new Error(`${description} not found.`);
  }
  return element;
}

function tile(label: string) {
  const summary = screen.getByRole('region', { name: 'Summary' });
  return found(within(summary).getByText(label).parentElement, `The "${label}" tile`);
}

function card(location: string) {
  return found(
    screen.getByRole('link', { name: location }).closest('article'),
    `The ${location} card`,
  );
}

describe('OverviewPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(now);
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('latest values', () => {
    it('shows the latest values in the summary tiles', async () => {
      renderOverview();

      expect(await screen.findByText('Latest samples received at 14:29:47')).toBeInTheDocument();
      expect(tile('Energy, all locations')).toHaveTextContent(
        'Energy, all locations3,012.52Sum of the latest reading per location',
      );
      expect(tile('Average CO₂')).toHaveTextContent(
        'Average CO₂672 ppmPM2.5 19 µg/m³ · humidity 45 %',
      );
      expect(tile('Motion now')).toHaveTextContent('Motion now2 of 6 roomsLiving Room, Garage');
      expect(await screen.findByText('79,836')).toBeInTheDocument();
      expect(tile('Samples, last 24 h')).toHaveTextContent('77 % of expected polls succeeded');
    });

    it("shows each location's values in a card that links to its analytics", async () => {
      const user = userEvent.setup();
      const router = renderOverview();

      await screen.findByRole('link', { name: 'Kitchen' });
      expect(screen.getAllByRole('article')).toHaveLength(6);
      const kitchen = card('Kitchen');
      expect(within(kitchen).getByRole('heading', { level: 3, name: 'Kitchen' })).toBeVisible();
      expect(kitchen).toHaveTextContent('No motion');
      expect(kitchen).toHaveTextContent('Energy845.62');
      expect(kitchen).toHaveTextContent('CO₂803 ppmPM2.525 µg/m³Humidity36 %');
      expect(card('Living Room')).toHaveTextContent('Motion');
      expect(card('Living Room')).not.toHaveTextContent('No motion');

      await user.click(screen.getByRole('link', { name: 'Kitchen' }));

      expect(router.state.location.pathname).toBe('/analytics');
      expect(router.state.location.search).toBe('?location=KITCHEN');
    });

    it('draws the energy trend of the last 2 hours in each card', async () => {
      renderOverview();

      const trend = await within(
        await screen.findByRole('region', { name: 'Locations' }),
      ).findAllByRole('img', { name: 'Energy trend over the last 2 hours' });
      expect(trend).toHaveLength(1);
      expect(within(card('Kitchen')).getByRole('img')).toBe(trend[0]);
      expect(within(card('Office')).getByRole('img')).toHaveAccessibleName(
        'No energy samples in the last 2 hours',
      );
    });

    it.each([
      ['CO₂ at 800 ppm', { co2: 800 }, null],
      ['CO₂ at 801 ppm', { co2: 801 }, 'CO₂ above 800 ppm'],
      ['PM2.5 at 35 µg/m³', { pm25: 35 }, null],
      ['PM2.5 at 36 µg/m³', { pm25: 36 }, 'PM2.5 above 35 µg/m³'],
    ])('warns about %s: %s', async (_, sample, warning) => {
      renderOverview({
        latest: [
          latestReadingsMock(latestReadings(fresh, { OFFICE: { co2: 700, pm25: 20, ...sample } })),
        ],
      });

      await screen.findByRole('link', { name: 'Office' });
      const office = card('Office');
      if (warning) {
        expect(office).toHaveTextContent(warning);
      } else {
        expect(office).not.toHaveTextContent(/above/);
      }
    });

    it('shows both warnings when CO₂ and PM2.5 are high', async () => {
      renderOverview();

      await screen.findByRole('link', { name: 'Garage' });
      expect(card('Garage')).toHaveTextContent('CO₂ above 800 ppm');
      expect(card('Garage')).toHaveTextContent('PM2.5 above 35 µg/m³');
    });

    it('shows a dash for a meter without a reading, and no chip for a missing motion sensor', async () => {
      const readings = latestReadings(fresh).filter(
        (reading) => !(reading.location === 'GARAGE' && reading.__typename !== 'EnergyReading'),
      );
      renderOverview({ latest: [latestReadingsMock(readings)] });

      await screen.findByRole('link', { name: 'Garage' });
      const garage = card('Garage');
      expect(garage).toHaveTextContent('CO₂— ppmPM2.5— µg/m³Humidity— %');
      expect(garage).not.toHaveTextContent(/motion|above/i);
      expect(tile('Motion now')).toHaveTextContent('1 of 5 rooms');
    });

    it('says when no room has motion', async () => {
      renderOverview({
        latest: [
          latestReadingsMock(
            latestReadings(fresh, {
              LIVING_ROOM: { motionDetected: false },
              GARAGE: { motionDetected: false },
            }),
          ),
        ],
      });

      await screen.findByText('Latest samples received at 14:29:47');
      expect(tile('Motion now')).toHaveTextContent('Motion now0 of 6 roomsNo motion detected');
    });

    it('says so when no readings are stored yet', async () => {
      renderOverview({
        latest: [latestReadingsMock([])],
        // Without readings the count may come back without a row.
        sampleCount: [{ request: sampleCount.request, result: { data: { readingCounts: [] } } }],
      });

      expect(
        await screen.findByRole('heading', { level: 3, name: 'No readings stored yet' }),
      ).toBeInTheDocument();
      expect(tile('Energy, all locations')).toHaveTextContent('—No readings yet');
      expect(tile('Average CO₂')).toHaveTextContent('—No readings yet');
      expect(tile('Motion now')).toHaveTextContent('—No readings yet');
      expect(await screen.findByText('0 % of expected polls succeeded')).toBeInTheDocument();
      expect(screen.queryByText(/Latest samples received at/)).toBeNull();
      expect(screen.queryByText(/No new samples/)).toBeNull();
    });
  });

  describe('freshness', () => {
    it('does not warn about samples exactly 2 minutes old', async () => {
      renderOverview({ latest: [latestReadingsMock(latestReadings('2026-11-15T11:28:00Z'))] });

      await screen.findByText('Latest samples received at 14:28:00');
      expect(screen.queryByText(/No new samples/)).toBeNull();
    });

    it('warns once for the page when the latest samples are older than 2 minutes', async () => {
      renderOverview({ latest: [latestReadingsMock(latestReadings('2026-11-15T11:25:47Z'))] });

      expect(await screen.findByText(/No new samples/)).toHaveTextContent(
        'No new samples for 4 min. WeakApp may be unavailable; the values shown were received at 14:25:47.',
      );
      expect(screen.getAllByText(/No new samples/)).toHaveLength(1);
    });
  });

  describe('trends', () => {
    it('draws the average energy of the last 24 hours, with its gaps', async () => {
      renderOverview();

      const energy = screen.getByRole('region', { name: 'Energy, last 24 hours' });
      expect(
        await within(energy).findByRole('img', {
          name: 'Average energy of all locations per 15 minutes over the last 24 hours, from 400.00 to 495.00. No samples in 1 period.',
        }),
      ).toBeInTheDocument();
      expect(energy).toHaveTextContent('Average of all locations per 15 minutes');
      expect(energy).toHaveTextContent('AverageNo samples');
    });

    it("shows a bucket's details on hover", async () => {
      renderOverview();

      const energy = screen.getByRole('region', { name: 'Energy, last 24 hours' });
      await within(energy).findByRole('img');
      const plot = found(energy.querySelector('.recharts-wrapper'), 'The energy chart');
      fireEvent.mouseMove(plot, { clientX: 400, clientY: 100 });

      expect(await within(energy).findByText('Min – max')).toBeInTheDocument();
      expect(energy).toHaveTextContent('12.50 – 989.25');
    });

    it('leaves "No samples" out of the legend when no bucket is missing', async () => {
      renderOverview({
        energyTrend: [
          energyTrendMock(
            lastDay,
            Array.from({ length: 96 }, (_, index) => ({
              bucketStart: isoAt(Date.parse('2026-11-14T11:30:00Z') + index * 15 * 60_000),
              average: 450,
              minimum: 12.5,
              maximum: 989.25,
              samples: 69,
            })),
          ),
        ],
      });

      const energy = screen.getByRole('region', { name: 'Energy, last 24 hours' });
      expect(
        await within(energy).findByRole('img', { name: /from 450\.00 to 450\.00\.$/ }),
      ).toBeInTheDocument();
      expect(energy).not.toHaveTextContent('No samples');
    });

    it('says so when the last 24 hours have no energy samples', async () => {
      renderOverview({ energyTrend: [energyTrendMock(lastDay, [])] });

      const energy = screen.getByRole('region', { name: 'Energy, last 24 hours' });
      expect(
        await within(energy).findByRole('heading', {
          name: 'No energy samples in the last 24 hours',
        }),
      ).toBeInTheDocument();
    });

    it('shows the stored share of expected samples per hour', async () => {
      renderOverview();

      const coverageCard = screen.getByRole('region', { name: 'Ingestion coverage' });
      const bars = await within(coverageCard).findByRole('img', {
        name: 'Stored samples per hour as a share of the expected polls over the last 24 hours: 76 % overall, lowest 41 % at 05:00–06:00.',
      });
      expect(coverageCard).toHaveTextContent(
        'Stored samples per hour as a share of the expected 240 polls',
      );
      expect(bars.children).toHaveLength(25);
      // The range starts at 14:30, so the first bar is its half of the 14:00 hour.
      expect(bars.children[0]).toHaveAttribute(
        'title',
        '14 Nov 14:30–15:00 · 554 of 720 samples (77 %)',
      );
      expect(bars.children[15]).toHaveAttribute(
        'title',
        '15 Nov 05:00–06:00 · 590 of 1,440 samples (41 %)',
      );
      expect(coverageCard).toHaveTextContent(
        "05:00–06:00 is lowest at 41 %: most polls failed or their samples weren't stored.",
      );
      expect(coverageCard).toHaveTextContent('100 %50 %0 %18:0000:0006:0012:00');
    });

    it('calls out no hour when none is below half of the expected samples', async () => {
      renderOverview({
        coverage: [
          ingestionCoverageMock(
            lastDay,
            Array.from({ length: 25 }, (_, index) => ({
              bucketStart: isoAt(Date.parse('2026-11-14T11:00:00Z') + index * HOUR_MS),
              samples: 1109,
            })),
          ),
        ],
      });

      const coverageCard = screen.getByRole('region', { name: 'Ingestion coverage' });
      expect(
        await within(coverageCard).findByRole('img', {
          name: 'Stored samples per hour as a share of the expected polls over the last 24 hours: 80 % overall.',
        }),
      ).toBeInTheDocument();
      expect(coverageCard).not.toHaveTextContent(/is lowest/);
    });

    it('marks the charts busy while they load for the first time', () => {
      renderOverview();

      expect(screen.getByRole('region', { name: 'Energy, last 24 hours' })).toHaveAttribute(
        'aria-busy',
        'true',
      );
      expect(screen.getByRole('region', { name: 'Ingestion coverage' })).toHaveAttribute(
        'aria-busy',
        'true',
      );
    });

    it.each([
      ['energyTrend', energyTrend, 'Energy, last 24 hours'],
      ['coverage', coverage, 'Ingestion coverage'],
    ] as const)(
      'shows a failed %s chart in its card and loads it again on Retry',
      async (operation, response, title) => {
        const user = userEvent.setup();
        renderOverview({ [operation]: [failing(response), response] });

        const chartCard = screen.getByRole('region', { name: title });
        expect(
          await within(chartCard).findByText("This chart couldn't be loaded."),
        ).toBeInTheDocument();
        expect(await screen.findByText('3,012.52')).toBeInTheDocument();

        await user.click(within(chartCard).getByRole('button', { name: 'Retry' }));

        expect(await within(chartCard).findByRole('img')).toBeInTheDocument();
      },
    );
  });

  describe('loading and errors', () => {
    it('marks the widgets busy while they load for the first time', () => {
      renderOverview();

      expect(screen.getByRole('region', { name: 'Locations' })).toHaveAttribute(
        'aria-busy',
        'true',
      );
      expect(tile('Energy, all locations')).toHaveAttribute('aria-busy', 'true');
      expect(tile('Samples, last 24 h')).toHaveAttribute('aria-busy', 'true');
    });

    it('shows a failed sample count in its tile and loads it again on Retry', async () => {
      const user = userEvent.setup();
      renderOverview({ sampleCount: [failing(sampleCount), sampleCount] });

      expect(await screen.findByText("The sample count couldn't be loaded.")).toBeInTheDocument();
      expect(await screen.findByText('3,012.52')).toBeInTheDocument();

      await user.click(within(tile('Samples, last 24 h')).getByRole('button', { name: 'Retry' }));

      expect(await screen.findByText('79,836')).toBeInTheDocument();
    });

    it('shows failed latest readings once in the Locations section while the rest keeps working', async () => {
      const user = userEvent.setup();
      renderOverview({ latest: [failing(latest), latest] });

      const locations = screen.getByRole('region', { name: 'Locations' });
      expect(
        await within(locations).findByText("The latest readings couldn't be loaded."),
      ).toBeInTheDocument();
      expect(tile('Energy, all locations')).toHaveTextContent("—Couldn't load");
      expect(tile('Motion now')).toHaveTextContent("—Couldn't load");
      expect(await screen.findByText('79,836')).toBeInTheDocument();
      expect(screen.queryByText(/Latest samples received at/)).toBeNull();
      expect(within(locations).getAllByRole('button', { name: 'Retry' })).toHaveLength(1);
      expect(within(tile('Energy, all locations')).queryByRole('button')).toBeNull();

      await user.click(within(locations).getByRole('button', { name: 'Retry' }));

      expect(await screen.findByText('Latest samples received at 14:29:47')).toBeInTheDocument();
      expect(tile('Energy, all locations')).toHaveTextContent('3,012.52');
    });

    it('keeps the location cards when the energy trends fail', async () => {
      renderOverview({ sparklines: [failing(sparklines)] });

      expect(await screen.findByText("The energy trends couldn't be loaded.")).toBeInTheDocument();
      expect(card('Kitchen')).toHaveTextContent('845.62');
      expect(within(card('Kitchen')).queryByRole('img')).toBeNull();
    });

    it('shows one page error when every query failed, and loads everything again on Retry', async () => {
      const user = userEvent.setup();
      renderOverview({
        latest: [failing(latest), latest],
        sampleCount: [failing(sampleCount), sampleCount],
        sparklines: [failing(sparklines), sparklines],
        energyTrend: [failing(energyTrend), energyTrend],
        coverage: [failing(coverage), coverage],
      });

      expect(await screen.findByRole('alert')).toHaveTextContent(
        "Couldn't load the dashboardThe API gateway didn't answer. The page tries again every 30 s.",
      );
      expect(screen.queryByRole('region', { name: 'Summary' })).toBeNull();
      expect(screen.getAllByRole('button', { name: 'Retry' })).toHaveLength(1);

      await user.click(screen.getByRole('button', { name: 'Retry' }));

      expect(await screen.findByText('79,836')).toBeInTheDocument();
      expect(await screen.findByText('Latest samples received at 14:29:47')).toBeInTheDocument();
      expect(
        await within(screen.getByRole('region', { name: 'Ingestion coverage' })).findByRole('img'),
      ).toBeInTheDocument();
      expect(screen.queryByRole('alert')).toBeNull();
    });
  });

  it('says how often meters are polled', async () => {
    renderOverview();

    expect(
      await screen.findByText(
        'Every value is a sample: meters are polled every 15 s and stamped with the time they were received.',
      ),
    ).toBeInTheDocument();
  });
});
