import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  defaultFilterVariables,
  energyReadings,
  failed,
  matchCountsMock,
  reading,
  readingsMock,
  rejected,
  type ReadingFilterVariables,
} from '../../test/readingsMocks';
import { renderRoute } from '../../test/renderRoute';

// 14:29:47 in Moscow: the default range is 13:30–14:30.
const opened = new Date('2026-11-15T11:29:47Z');

const counts = { AIR_QUALITY: 1110, ENERGY: 1110, MOTION: 1110 };

const firstPage = readingsMock(defaultFilterVariables, energyReadings(1, 3));

const energyOnly: ReadingFilterVariables = { ...defaultFilterVariables, types: ['ENERGY'] };
const energyOnlySearch =
  '?from=2026-11-15T10%3A30%3A00.000Z&to=2026-11-15T11%3A30%3A00.000Z&type=ENERGY';

function filters() {
  return screen.getByRole('region', { name: 'Filters' });
}

function results() {
  return screen.getByRole('region', { name: 'Results' });
}

/** The table's body rows by their first cell, the reception time. */
function receivedTimes() {
  return within(results())
    .getAllByRole('row')
    .slice(1)
    .map((row) => within(row).getAllByRole('cell')[0]?.textContent);
}

/** Waits for the footer line under the table, "Showing 25 of 3,330 readings". */
async function expectShown(text: string | RegExp) {
  await waitFor(() => {
    expect(within(results()).getByText(/^Showing/, { selector: 'p' })).toHaveTextContent(text);
  });
}

function countLabel(type: string) {
  return within(filters()).getByText(type, { selector: 'p span' });
}

describe('ReadingsPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(opened);
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('match counts', () => {
    it('shows the total and the count per type', async () => {
      renderRoute('/readings', {
        mocks: [matchCountsMock(defaultFilterVariables, counts), firstPage],
      });

      expect(await within(filters()).findByText('readings match')).toHaveTextContent(
        '3,330 readings match',
      );
      expect(countLabel('Air quality')).toHaveTextContent('Air quality 1,110');
      expect(countLabel('Energy')).toHaveTextContent('Energy 1,110');
    });

    it('counts 0 for a selected type without readings', async () => {
      const variables: ReadingFilterVariables = {
        ...defaultFilterVariables,
        types: ['ENERGY', 'MOTION'],
      };
      renderRoute(
        '/readings?from=2026-11-15T10:30:00Z&to=2026-11-15T11:30:00Z&type=ENERGY&type=MOTION',
        {
          mocks: [
            matchCountsMock(variables, { ENERGY: 1 }),
            readingsMock(variables, energyReadings(1, 1)),
          ],
        },
      );

      expect(await within(filters()).findByText('reading matches')).toHaveTextContent(
        '1 reading matches',
      );
      expect(countLabel('Motion')).toHaveTextContent('Motion 0');
      expect(within(filters()).queryByText('Air quality', { selector: 'p span' })).toBeNull();
    });

    it('leaves out the breakdown for one type', async () => {
      const variables: ReadingFilterVariables = { ...energyOnly, locations: ['KITCHEN'] };
      renderRoute(`/readings${energyOnlySearch}&location=KITCHEN`, {
        mocks: [
          matchCountsMock(variables, { ENERGY: 240 }),
          readingsMock(variables, energyReadings(1, 1)),
        ],
      });

      expect(await within(filters()).findByText('readings match')).toHaveTextContent(
        '240 readings match',
      );
      expect(within(filters()).queryByText('Energy', { selector: 'p span' })).toBeNull();
    });

    it('shows its own error and loads again on Retry', async () => {
      const user = userEvent.setup();
      const mock = matchCountsMock(defaultFilterVariables, counts);
      renderRoute('/readings', { mocks: [failed(mock), mock, firstPage] });

      await user.click(await within(filters()).findByRole('button', { name: 'Retry' }));

      expect(await within(filters()).findByText('readings match')).toHaveTextContent(
        '3,330 readings match',
      );
      expect(screen.queryByRole('alert')).toBeNull();
    });
  });

  describe('table', () => {
    it('shows each reading type with its values, newest first', async () => {
      const airQuality = reading('a', '2026-11-15T11:29:47Z', 'LIVING_ROOM', {
        __typename: 'AirQualityReading',
        co2: 803,
        pm25: 25,
        humidity: 36,
      });
      const energy = reading('e', '2026-11-15T11:29:47Z', 'KITCHEN', {
        __typename: 'EnergyReading',
        energy: 1234.5,
      });
      const moving = reading('m1', '2026-11-15T11:29:32Z', 'GARAGE', {
        __typename: 'MotionReading',
        motionDetected: true,
      });
      const still = reading('m2', '2026-11-15T11:29:32Z', 'OFFICE', {
        __typename: 'MotionReading',
        motionDetected: false,
      });
      renderRoute('/readings', {
        mocks: [
          matchCountsMock(defaultFilterVariables, { AIR_QUALITY: 1, ENERGY: 1, MOTION: 2 }),
          readingsMock(defaultFilterVariables, [airQuality, energy, moving, still]),
        ],
      });

      const table = await within(results()).findByRole('table', {
        name: 'Readings, newest first',
      });
      const cells = within(table)
        .getAllByRole('row')
        .slice(1)
        .map((row) =>
          within(row)
            .getAllByRole('cell')
            .map((cell) => cell.textContent),
        );

      expect(cells).toEqual([
        [
          '15 Nov 14:29:47',
          'Air quality',
          'Living Room',
          'CO₂ 803 ppm · PM2.5 25 µg/m³ · Humidity 36 %',
        ],
        ['15 Nov 14:29:47', 'Energy', 'Kitchen', '1,234.50'],
        ['15 Nov 14:29:32', 'Motion', 'Garage', 'Motion detected'],
        ['15 Nov 14:29:32', 'Motion', 'Office', 'No motion'],
      ]);
      await expectShown('Showing 4 of 4 readings');
      expect(within(results()).queryByRole('button', { name: /more/ })).toBeNull();
    });

    it('appends the next page after the last cursor, without duplicates', async () => {
      const user = userEvent.setup();
      renderRoute('/readings', {
        mocks: [
          matchCountsMock(defaultFilterVariables, { ENERGY: 5 }),
          readingsMock(defaultFilterVariables, energyReadings(1, 3), {}, true),
          readingsMock(defaultFilterVariables, energyReadings(4, 5), { after: 'e3' }),
        ],
      });

      await user.click(await within(results()).findByRole('button', { name: 'Load 25 more' }));

      await expectShown('Showing 5 of 5 readings');
      expect(receivedTimes()).toEqual([
        '15 Nov 14:29:46',
        '15 Nov 14:29:45',
        '15 Nov 14:29:44',
        '15 Nov 14:29:43',
        '15 Nov 14:29:42',
      ]);
      expect(within(results()).queryByRole('button', { name: /more/ })).toBeNull();
    });

    it('sizes the next page by Load at a time without reloading the loaded ones', async () => {
      const user = userEvent.setup();
      renderRoute('/readings', {
        mocks: [
          matchCountsMock(defaultFilterVariables, counts),
          readingsMock(defaultFilterVariables, energyReadings(1, 2), {}, true),
          readingsMock(defaultFilterVariables, energyReadings(3, 4), { first: 100, after: 'e2' }),
        ],
      });
      await within(results()).findByRole('button', { name: 'Load 25 more' });

      // No mock answers `first: 100` without a cursor: a request would show the table's error.
      await user.selectOptions(screen.getByRole('combobox', { name: 'Load at a time' }), '100');
      expect(receivedTimes()).toHaveLength(2);

      await user.click(within(results()).getByRole('button', { name: 'Load 100 more' }));

      await expectShown('Showing 4 of 3,330 readings');
    });

    it('starts from the first page when the filter changes', async () => {
      const user = userEvent.setup();
      renderRoute('/readings', {
        mocks: [
          matchCountsMock(defaultFilterVariables, counts),
          readingsMock(defaultFilterVariables, energyReadings(1, 2), {}, true),
          readingsMock(defaultFilterVariables, energyReadings(3, 4), { after: 'e2' }, true),
          matchCountsMock(energyOnly, { ENERGY: 1110 }),
          readingsMock(energyOnly, energyReadings(11, 11), {}, true),
        ],
      });
      await user.click(await within(results()).findByRole('button', { name: 'Load 25 more' }));
      await expectShown(/^Showing 4 /);

      await user.click(
        within(screen.getByRole('group', { name: 'Meter types' })).getByRole('button', {
          name: 'Air quality',
        }),
      );
      await user.click(
        within(screen.getByRole('group', { name: 'Meter types' })).getByRole('button', {
          name: 'Motion',
        }),
      );
      await user.click(screen.getByRole('button', { name: 'Apply' }));

      await expectShown('Showing 1 of 1,110 readings');
      expect(receivedTimes()).toEqual(['15 Nov 14:29:36']);
    });

    it('keeps the loaded rows when the next page fails, and tries again', async () => {
      const user = userEvent.setup();
      const nextPage = readingsMock(defaultFilterVariables, energyReadings(3, 3), { after: 'e2' });
      renderRoute('/readings', {
        mocks: [
          matchCountsMock(defaultFilterVariables, counts),
          readingsMock(defaultFilterVariables, energyReadings(1, 2), {}, true),
          failed(nextPage),
          nextPage,
        ],
      });

      await user.click(await within(results()).findByRole('button', { name: 'Load 25 more' }));

      const button = await within(results()).findByRole('button', {
        name: 'Load 25 more',
        description: "More readings couldn't be loaded.",
      });
      expect(receivedTimes()).toHaveLength(2);

      await user.click(button);

      await expectShown(/^Showing 3 /);
      expect(within(results()).queryByText("More readings couldn't be loaded.")).toBeNull();
    });

    it('shows the count of loaded readings alone while the total is unknown', async () => {
      renderRoute('/readings', {
        mocks: [failed(matchCountsMock(defaultFilterVariables, counts)), firstPage],
      });

      await expectShown('Showing 3 readings');
    });

    it('says when nothing matches and offers to reset the filters', async () => {
      const user = userEvent.setup();
      const router = renderRoute(`/readings${energyOnlySearch}`, {
        mocks: [matchCountsMock(energyOnly, {}), readingsMock(energyOnly, [])],
      });

      expect(
        await within(results()).findByRole('heading', {
          name: 'No readings between 15 Nov 13:30 and 15 Nov 14:30',
        }),
      ).toBeInTheDocument();

      await user.click(within(results()).getByRole('button', { name: 'Reset filters' }));

      expect(router.state.location.search).not.toContain('type=');
    });

    it('shows its own error and loads again on Retry', async () => {
      const user = userEvent.setup();
      renderRoute('/readings', {
        mocks: [matchCountsMock(defaultFilterVariables, counts), failed(firstPage), firstPage],
      });

      await user.click(await within(results()).findByRole('button', { name: 'Retry' }));

      await expectShown('Showing 3 of 3,330 readings');
    });

    it('keeps the previous rows dimmed while another filter loads', async () => {
      const user = userEvent.setup();
      renderRoute('/readings', {
        mocks: [
          matchCountsMock(defaultFilterVariables, counts),
          firstPage,
          { ...readingsMock(energyOnly, energyReadings(11, 11)), delay: Infinity },
          { ...matchCountsMock(energyOnly, counts), delay: Infinity },
        ],
      });
      await expectShown(/^Showing/);

      await user.click(
        within(screen.getByRole('group', { name: 'Meter types' })).getByRole('button', {
          name: 'Motion',
        }),
      );
      await user.click(
        within(screen.getByRole('group', { name: 'Meter types' })).getByRole('button', {
          name: 'Air quality',
        }),
      );
      await user.click(screen.getByRole('button', { name: 'Apply' }));

      expect(await within(results()).findByRole('progressbar')).toBeInTheDocument();
      expect(receivedTimes()).toHaveLength(3);
    });
  });

  describe('page', () => {
    it('shows one error when every query failed and loads all again on Retry', async () => {
      const user = userEvent.setup();
      const countsMock = matchCountsMock(defaultFilterVariables, counts);
      renderRoute('/readings', {
        mocks: [failed(countsMock), failed(firstPage), countsMock, firstPage],
      });

      const pageError = await screen.findByRole('alert');
      expect(pageError).toHaveTextContent("Couldn't load the dashboard");
      expect(screen.queryByRole('region', { name: 'Results' })).toBeNull();
      expect(within(filters()).queryByRole('button', { name: 'Retry' })).toBeNull();

      await user.click(within(pageError).getByRole('button', { name: 'Retry' }));

      await expectShown('Showing 3 of 3,330 readings');
    });

    it('shows a rejected filter at the filters, not as a page error', async () => {
      renderRoute('/readings', {
        mocks: [rejected(matchCountsMock(defaultFilterVariables, counts)), rejected(firstPage)],
      });

      expect(await within(filters()).findByRole('alert')).toHaveTextContent(
        "The gateway didn't accept these filters.",
      );
      expect(screen.getAllByRole('alert')).toHaveLength(1);
    });

    it('loads the same filter again from the first page when it is applied again', async () => {
      const user = userEvent.setup();
      const router = renderRoute('/readings', {
        mocks: [
          matchCountsMock(defaultFilterVariables, counts),
          readingsMock(defaultFilterVariables, energyReadings(2, 3), {}, true),
          readingsMock(defaultFilterVariables, energyReadings(4, 4), { after: 'e3' }),
          matchCountsMock(defaultFilterVariables, { ...counts, ENERGY: 1111 }),
          readingsMock(defaultFilterVariables, energyReadings(1, 3), {}, true),
        ],
      });
      await user.click(await within(results()).findByRole('button', { name: 'Load 25 more' }));
      await expectShown(/^Showing 3 /);

      await user.click(screen.getByRole('button', { name: 'Apply' }));
      await user.click(screen.getByRole('button', { name: 'Apply' }));

      expect(await within(filters()).findByText('3,331')).toBeInTheDocument();
      await expectShown('Showing 3 of 3,331 readings');
      expect(receivedTimes()[0]).toBe('15 Nov 14:29:46');
      expect(router.state.historyAction).toBe('REPLACE');
    });

    it('returns to the loaded pages of a filter with the back button', async () => {
      const user = userEvent.setup();
      const router = renderRoute('/readings', {
        mocks: [
          matchCountsMock(defaultFilterVariables, counts),
          readingsMock(defaultFilterVariables, energyReadings(1, 2), {}, true),
          readingsMock(defaultFilterVariables, energyReadings(3, 3), { after: 'e2' }),
          matchCountsMock(energyOnly, counts),
          readingsMock(energyOnly, energyReadings(11, 11)),
        ],
      });
      await user.click(await within(results()).findByRole('button', { name: 'Load 25 more' }));
      await expectShown(/^Showing 3 /);
      await act(() => router.navigate(`/readings${energyOnlySearch}`));
      await expectShown(/^Showing 1 /);

      await act(() => router.navigate(-1));

      await expectShown(/^Showing 3 /);
    });
  });
});
