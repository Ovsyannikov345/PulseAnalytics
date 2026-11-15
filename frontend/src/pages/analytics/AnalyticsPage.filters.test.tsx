import { act, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { renderRoute } from '../../test/renderRoute';

// 14:30:00 in Moscow.
const opened = new Date('2026-11-15T11:30:00Z');

function rangeButton(name: string) {
  return within(screen.getByRole('group', { name: 'Time range' })).getByRole('button', { name });
}

function summary() {
  return within(screen.getByRole('region', { name: 'Filters' })).getByText(/ buckets\./);
}

describe('AnalyticsPage filters', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(opened);
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('opens with the location from an Overview link', () => {
    renderRoute('/analytics?location=KITCHEN');

    expect(screen.getByRole('heading', { level: 1, name: 'Analytics' })).toBeInTheDocument();
    expect(rangeButton('24 h')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('combobox', { name: 'Locations' })).toHaveDisplayValue('Kitchen');
  });

  it('keeps applied filters in the URL', async () => {
    const user = userEvent.setup();
    const router = renderRoute('/analytics?location=KITCHEN');

    await user.click(rangeButton('7 d'));
    expect(router.state.location.search).toBe('?range=7d&bucket=auto&location=KITCHEN');

    await user.selectOptions(screen.getByRole('combobox', { name: 'Bucket' }), '1 day');
    expect(router.state.location.search).toBe('?range=7d&bucket=DAY&location=KITCHEN');
  });

  it('opens a link with every filter', () => {
    renderRoute(
      '/analytics?range=custom&from=2026-11-01T06:00:00Z&to=2026-11-08T06:00:00Z&bucket=HOUR',
    );

    expect(rangeButton('Custom…')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('combobox', { name: 'Bucket' })).toHaveDisplayValue('1 hour');
    expect(summary()).toHaveTextContent('1 Nov 09:00 – 8 Nov 09:00 · 168 buckets.');
  });

  it('restores the previous filters with the back button', async () => {
    const user = userEvent.setup();
    const router = renderRoute('/analytics');

    await user.click(rangeButton('6 h'));
    expect(rangeButton('6 h')).toHaveAttribute('aria-pressed', 'true');

    await act(() => router.navigate(-1));

    expect(summary()).toHaveTextContent('14 Nov 14:30 – 15 Nov 14:30');
    expect(rangeButton('24 h')).toHaveAttribute('aria-pressed', 'true');
  });

  it('returns to the window a filter was applied with', async () => {
    const user = userEvent.setup();
    const router = renderRoute('/analytics');

    vi.setSystemTime(opened.getTime() + 5 * 60_000);
    await user.click(rangeButton('6 h'));
    vi.setSystemTime(opened.getTime() + 10 * 60_000);
    await user.click(rangeButton('1 h'));
    expect(summary()).toHaveTextContent('15 Nov 13:40 – 15 Nov 14:40');

    await act(() => router.navigate(-1));

    expect(summary()).toHaveTextContent('15 Nov 08:35 – 15 Nov 14:35');
  });

  it('ends a preset when it is applied, not when the page renders again', async () => {
    const user = userEvent.setup();
    const router = renderRoute('/analytics?range=24h&bucket=auto');
    expect(summary()).toHaveTextContent('14 Nov 14:30 – 15 Nov 14:30');

    vi.setSystemTime(opened.getTime() + 5 * 60_000);
    await user.click(rangeButton('Custom…'));
    expect(summary()).toHaveTextContent('14 Nov 14:30 – 15 Nov 14:30');

    await user.click(rangeButton('24 h'));
    expect(summary()).toHaveTextContent('14 Nov 14:35 – 15 Nov 14:35');
    // The same filters again replace the history entry instead of adding one.
    expect(router.state.historyAction).toBe('REPLACE');
  });
});
