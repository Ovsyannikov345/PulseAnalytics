import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  defaultFilterVariables,
  failed,
  matchCountsMock,
  rejected,
} from '../../test/readingsMocks';
import { renderRoute } from '../../test/renderRoute';

// 14:29:47 in Moscow: the default range is 13:30–14:30.
const opened = new Date('2026-11-15T11:29:47Z');

const counts = { AIR_QUALITY: 1110, ENERGY: 1110, MOTION: 1110 };

function filters() {
  return screen.getByRole('region', { name: 'Filters' });
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
      renderRoute('/readings', { mocks: [matchCountsMock(defaultFilterVariables, counts)] });

      expect(await within(filters()).findByText('readings match')).toHaveTextContent(
        '3,330 readings match',
      );
      expect(within(filters()).getByText('Air quality', { selector: 'p span' })).toHaveTextContent(
        'Air quality 1,110',
      );
      expect(within(filters()).getByText('Energy', { selector: 'p span' })).toHaveTextContent(
        'Energy 1,110',
      );
    });

    it('counts 0 for a selected type without readings', async () => {
      const variables = { ...defaultFilterVariables, types: ['ENERGY', 'MOTION'] as const };
      renderRoute(
        '/readings?from=2026-11-15T10:30:00Z&to=2026-11-15T11:30:00Z&type=ENERGY&type=MOTION',
        { mocks: [matchCountsMock({ ...variables, types: [...variables.types] }, { ENERGY: 1 })] },
      );

      expect(await within(filters()).findByText('reading matches')).toHaveTextContent(
        '1 reading matches',
      );
      expect(within(filters()).getByText('Motion', { selector: 'p span' })).toHaveTextContent(
        'Motion 0',
      );
      expect(within(filters()).queryByText('Air quality', { selector: 'p span' })).toBeNull();
    });

    it('leaves out the breakdown for one type', async () => {
      renderRoute(
        '/readings?from=2026-11-15T10:30:00Z&to=2026-11-15T11:30:00Z&type=ENERGY&location=KITCHEN',
        {
          mocks: [
            matchCountsMock(
              { ...defaultFilterVariables, types: ['ENERGY'], locations: ['KITCHEN'] },
              { ENERGY: 240 },
            ),
          ],
        },
      );

      expect(await within(filters()).findByText('readings match')).toHaveTextContent(
        '240 readings match',
      );
      expect(within(filters()).queryByText('Energy', { selector: 'p span' })).toBeNull();
    });

    it('shows its own error and loads again on Retry', async () => {
      const user = userEvent.setup();
      const mock = matchCountsMock(defaultFilterVariables, counts);
      renderRoute('/readings', { mocks: [failed(mock), mock] });

      await user.click(await within(filters()).findByRole('button', { name: 'Retry' }));

      expect(await within(filters()).findByText('readings match')).toHaveTextContent(
        '3,330 readings match',
      );
      expect(screen.queryByRole('alert')).toBeNull();
    });

    it('loads the same filter again when it is applied again', async () => {
      const user = userEvent.setup();
      const router = renderRoute('/readings', {
        mocks: [
          matchCountsMock(defaultFilterVariables, counts),
          matchCountsMock(defaultFilterVariables, { ...counts, MOTION: 1111 }),
        ],
      });
      await within(filters()).findByText('readings match');

      await user.click(screen.getByRole('button', { name: 'Apply' }));
      await user.click(screen.getByRole('button', { name: 'Apply' }));

      expect(await within(filters()).findByText('3,331')).toBeInTheDocument();
      expect(router.state.historyAction).toBe('REPLACE');
    });
  });

  it('shows a rejected filter at the filters', async () => {
    renderRoute('/readings', {
      mocks: [rejected(matchCountsMock(defaultFilterVariables, counts))],
    });

    expect(await within(filters()).findByRole('alert')).toHaveTextContent(
      "The gateway didn't accept these filters.",
    );
  });
});
