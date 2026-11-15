import type { MockLink } from '@apollo/client/testing';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { latestReadings } from '../../test/overviewFixtures';
import {
  energySparklinesMock,
  failing,
  latestReadingsMock,
  sampleCountMock,
} from '../../test/overviewMocks';
import { renderRoute } from '../../test/renderRoute';

// 14:30:00 in Moscow (UTC+03:00).
const now = new Date('2026-11-15T11:30:00Z');
const fresh = '2026-11-15T11:29:47Z';

// The windows ending now, as the page sends them.
const lastDay = { from: '2026-11-14T14:30:00.000+03:00', to: '2026-11-15T14:30:00.000+03:00' };
const lastTwoHours = { from: '2026-11-15T12:30:00.000+03:00', to: '2026-11-15T14:30:00.000+03:00' };

// 79,836 of the 103,680 samples that 18 meters give in 24 h at one poll per 15 s.
const sampleCount = sampleCountMock(lastDay, 79_836);
const sparklines = energySparklinesMock(lastTwoHours, [
  { location: 'KITCHEN', bucketStart: '2026-11-15T09:30:00Z', average: 400 },
  { location: 'KITCHEN', bucketStart: '2026-11-15T09:35:00Z', average: 500 },
]);

function renderOverview(mocks: MockLink.MockedResponse[]) {
  return renderRoute('/', { mocks });
}

function found<T>(element: T | null, description: string): T {
  if (element === null) {
    throw new Error(`${description} not found.`);
  }
  return element;
}

/** The summary tile with this label (the label's parent). */
function tile(label: string) {
  const summary = screen.getByRole('region', { name: 'Summary' });
  return found(within(summary).getByText(label).parentElement, `The "${label}" tile`);
}

/** The location card whose link has this name. */
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

  it('shows the latest values in the summary tiles', async () => {
    renderOverview([latestReadingsMock(latestReadings(fresh)), sampleCount, sparklines]);

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
    const router = renderOverview([
      latestReadingsMock(latestReadings(fresh)),
      sampleCount,
      sparklines,
    ]);

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
    renderOverview([latestReadingsMock(latestReadings(fresh)), sampleCount, sparklines]);

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
    renderOverview([
      latestReadingsMock(latestReadings(fresh, { OFFICE: { co2: 700, pm25: 20, ...sample } })),
      sampleCount,
      sparklines,
    ]);

    await screen.findByRole('link', { name: 'Office' });
    const office = card('Office');
    if (warning) {
      expect(office).toHaveTextContent(warning);
    } else {
      expect(office).not.toHaveTextContent(/above/);
    }
  });

  it('shows a dash for a meter without a reading, and no chip for a missing motion sensor', async () => {
    const readings = latestReadings(fresh).filter(
      (reading) => !(reading.location === 'GARAGE' && reading.__typename !== 'EnergyReading'),
    );
    renderOverview([latestReadingsMock(readings), sampleCount, sparklines]);

    await screen.findByRole('link', { name: 'Garage' });
    const garage = card('Garage');
    expect(garage).toHaveTextContent('CO₂— ppmPM2.5— µg/m³Humidity— %');
    expect(garage).not.toHaveTextContent(/motion|above/i);
    expect(tile('Motion now')).toHaveTextContent('1 of 5 rooms');
  });

  it('says when no room has motion', async () => {
    renderOverview([
      latestReadingsMock(
        latestReadings(fresh, {
          LIVING_ROOM: { motionDetected: false },
          GARAGE: { motionDetected: false },
        }),
      ),
      sampleCount,
      sparklines,
    ]);

    await screen.findByText('Latest samples received at 14:29:47');
    expect(tile('Motion now')).toHaveTextContent('Motion now0 of 6 roomsNo motion detected');
  });

  it('shows both warnings when CO₂ and PM2.5 are high', async () => {
    renderOverview([latestReadingsMock(latestReadings(fresh)), sampleCount, sparklines]);

    await screen.findByRole('link', { name: 'Garage' });
    expect(card('Garage')).toHaveTextContent('CO₂ above 800 ppm');
    expect(card('Garage')).toHaveTextContent('PM2.5 above 35 µg/m³');
  });

  it('does not warn about samples exactly 2 minutes old', async () => {
    renderOverview([
      latestReadingsMock(latestReadings('2026-11-15T11:28:00Z')),
      sampleCount,
      sparklines,
    ]);

    await screen.findByText('Latest samples received at 14:28:00');
    expect(screen.queryByText(/No new samples/)).toBeNull();
  });

  it('warns once for the page when the latest samples are older than 2 minutes', async () => {
    renderOverview([
      latestReadingsMock(latestReadings('2026-11-15T11:25:47Z')),
      sampleCount,
      sparklines,
    ]);

    expect(await screen.findByText(/No new samples/)).toHaveTextContent(
      'No new samples for 4 min. WeakApp may be unavailable; the values shown were received at 14:25:47.',
    );
    expect(screen.getAllByText(/No new samples/)).toHaveLength(1);
  });

  it('marks the widgets busy while they load for the first time', () => {
    renderOverview([latestReadingsMock(latestReadings(fresh)), sampleCount, sparklines]);

    expect(screen.getByRole('region', { name: 'Locations' })).toHaveAttribute('aria-busy', 'true');
    expect(tile('Energy, all locations')).toHaveAttribute('aria-busy', 'true');
    expect(tile('Samples, last 24 h')).toHaveAttribute('aria-busy', 'true');
  });

  it('shows a failed sample count in its tile and loads it again on Retry', async () => {
    const user = userEvent.setup();
    renderOverview([
      latestReadingsMock(latestReadings(fresh)),
      failing(sampleCount),
      sampleCount,
      sparklines,
    ]);

    expect(await screen.findByText("The sample count couldn't be loaded.")).toBeInTheDocument();
    expect(await screen.findByText('3,012.52')).toBeInTheDocument();

    await user.click(within(tile('Samples, last 24 h')).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('79,836')).toBeInTheDocument();
  });

  it('shows failed latest readings once in the Locations section while the rest keeps working', async () => {
    const user = userEvent.setup();
    const latest = latestReadingsMock(latestReadings(fresh));
    renderOverview([failing(latest), latest, sampleCount, sparklines]);

    const locations = screen.getByRole('region', { name: 'Locations' });
    expect(
      await within(locations).findByText("The latest readings couldn't be loaded."),
    ).toBeInTheDocument();
    expect(tile('Energy, all locations')).toHaveTextContent("—Couldn't load");
    expect(tile('Motion now')).toHaveTextContent("—Couldn't load");
    expect(await screen.findByText('79,836')).toBeInTheDocument();
    expect(screen.queryByText(/Latest samples received at/)).toBeNull();
    expect(screen.getAllByRole('button', { name: 'Retry' })).toHaveLength(1);

    await user.click(within(locations).getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('Latest samples received at 14:29:47')).toBeInTheDocument();
    expect(tile('Energy, all locations')).toHaveTextContent('3,012.52');
  });

  it('keeps the location cards when the energy trends fail', async () => {
    renderOverview([latestReadingsMock(latestReadings(fresh)), sampleCount, failing(sparklines)]);

    expect(await screen.findByText("The energy trends couldn't be loaded.")).toBeInTheDocument();
    expect(card('Kitchen')).toHaveTextContent('845.62');
    expect(within(card('Kitchen')).queryByRole('img')).toBeNull();
  });

  it('shows one page error when every query failed, and loads everything again on Retry', async () => {
    const user = userEvent.setup();
    const latest = latestReadingsMock(latestReadings(fresh));
    renderOverview([
      failing(latest),
      failing(sampleCount),
      failing(sparklines),
      latest,
      sampleCount,
      sparklines,
    ]);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      "Couldn't load the dashboardThe API gateway didn't answer. The page tries again every 30 s.",
    );
    expect(screen.queryByRole('region', { name: 'Summary' })).toBeNull();

    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('79,836')).toBeInTheDocument();
    expect(await screen.findByText('Latest samples received at 14:29:47')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).toBeNull();
  });

  it('says so when no readings are stored yet', async () => {
    renderOverview([
      latestReadingsMock([]),
      // Without readings the count may come back without a row.
      {
        request: sampleCount.request,
        result: { data: { readingCounts: [] } },
      },
      sparklines,
    ]);

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

  it('says how often meters are polled', async () => {
    renderOverview([latestReadingsMock(latestReadings(fresh)), sampleCount, sparklines]);

    expect(
      await screen.findByText(
        'Every value is a sample: meters are polled every 15 s and stamped with the time they were received.',
      ),
    ).toBeInTheDocument();
  });
});
