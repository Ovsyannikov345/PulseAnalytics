import type { MockLink } from '@apollo/client/testing';
import { act, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { toIsoWithOffset } from '../../domain/utcOffset';
import { latestReadings } from '../../test/overviewFixtures';
import {
  energySparklinesMock,
  energyTrendMock,
  ingestionCoverageMock,
  latestReadingsMock,
  sampleCountMock,
} from '../../test/overviewMocks';
import { renderRoute } from '../../test/renderRoute';

const clock = vi.hoisted(() => {
  let current = new Date('2026-11-15T11:30:00Z');
  const listeners = new Set<() => void>();
  return {
    get: () => current,
    set: (next: Date) => {
      current = next;
      for (const listener of listeners) {
        listener();
      }
    },
    subscribe: (listener: () => void) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
  };
});

vi.mock('./useOverviewRefresh', async () => {
  const { useSyncExternalStore } = await import('react');
  return { useOverviewRefresh: () => useSyncExternalStore(clock.subscribe, clock.get) };
});

// In Moscow (UTC+03:00): the page opens at 14:30:00 and refreshes at 14:30:30.
const opened = new Date('2026-11-15T11:30:00Z');
const refreshed = new Date('2026-11-15T11:30:30Z');

const HOUR_MS = 3_600_000;

function windowMocks(end: Date, sampleCount = 79_836): MockLink.MockedResponse[] {
  const before = (hours: number) => toIsoWithOffset(new Date(end.getTime() - hours * HOUR_MS));
  const to = toIsoWithOffset(end);
  return [
    sampleCountMock({ from: before(24), to }, sampleCount),
    energySparklinesMock({ from: before(2), to }, []),
    energyTrendMock({ from: before(24), to }, []),
    ingestionCoverageMock({ from: before(24), to }, []),
  ];
}

describe('OverviewPage refresh', () => {
  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Moscow');
    clock.set(opened);
  });

  it('moves the time windows with the clock and keeps the values while the new ones load', async () => {
    renderRoute('/', {
      mocks: [
        latestReadingsMock(latestReadings('2026-11-15T11:29:47Z')),
        ...windowMocks(opened),
        ...windowMocks(refreshed, 79_900).map((mock) => ({ ...mock, delay: 200 })),
      ],
    });
    expect(await screen.findByText('79,836')).toBeInTheDocument();

    act(() => {
      clock.set(refreshed);
    });

    expect(screen.getByText('79,836')).toBeInTheDocument();
    expect(screen.getByText('Samples, last 24 h').parentElement).toHaveAttribute(
      'aria-busy',
      'false',
    );
    expect(await screen.findByText('79,900')).toBeInTheDocument();
  });

  it('checks the latest samples against the clock', async () => {
    const later = new Date('2026-11-15T11:32:00Z');
    renderRoute('/', {
      mocks: [
        latestReadingsMock(latestReadings('2026-11-15T11:29:47Z')),
        ...windowMocks(opened),
        ...windowMocks(later),
      ],
    });
    await screen.findByText('Latest samples received at 14:29:47');
    expect(screen.queryByText(/No new samples/)).toBeNull();

    act(() => {
      clock.set(later);
    });

    expect(await screen.findByText(/No new samples for 2 min/)).toBeInTheDocument();
  });
});
