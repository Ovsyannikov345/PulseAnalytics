import type { MockLink } from '@apollo/client/testing';
import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { FakeReadingsHub } from '../../test/fakeReadingsHub';
import { hubMessage, liveLatestReadingsMock, liveLatestRows } from '../../test/liveMocks';
import { failing } from '../../test/overviewMocks';
import { renderRoute } from '../../test/renderRoute';

// 14:30:00 in Moscow.
const now = new Date('2026-11-15T11:30:00Z');
const stored = '2026-11-15T11:29:47Z';
const pushed = '2026-11-15T11:30:02Z';

const latest = liveLatestReadingsMock(liveLatestRows(stored));

async function renderLive(mocks: MockLink.MockedResponse[] = [latest]) {
  const hub = new FakeReadingsHub();
  renderRoute('/live', { mocks, hubConnectionFactory: hub.factory });
  await waitFor(() => {
    expect(hub.connections).toHaveLength(1);
  });

  return hub;
}

async function settle(action: () => void) {
  await act(async () => {
    action();
    await Promise.resolve();
  });
}

async function renderConnected(mocks?: MockLink.MockedResponse[]) {
  const hub = await renderLive(mocks);
  await settle(() => {
    hub.connection.connect();
  });
  await screen.findByRole('list', { name: 'Rooms' });

  return hub;
}

function room(name: string) {
  const card = screen.getByRole('heading', { level: 2, name }).closest('article');
  if (!card) {
    throw new Error(`No card for ${name}`);
  }

  return within(card);
}

function status() {
  return screen.getByText(/^(Connecting…|Live|Reconnecting…|Offline)$/, { selector: 'output' });
}

describe('LivePage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(now);
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('connects first and shows skeletons until the values arrive', async () => {
    await renderLive();

    expect(await screen.findByRole('heading', { level: 1, name: 'Live' })).toBeInTheDocument();
    expect(status()).toHaveTextContent('Connecting…');
    expect(screen.getByLabelText('Loading the latest values')).toHaveAttribute('aria-busy', 'true');
    expect(screen.queryByRole('list', { name: 'Rooms' })).not.toBeInTheDocument();
  });

  it('loads the stored values once connected', async () => {
    await renderConnected();

    expect(status()).toHaveTextContent('Live');
    expect(await screen.findByText('Latest samples received at 14:29:47')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(6);
    expect(room('Kitchen').getByText('845.62')).toBeInTheDocument();
    expect(room('Kitchen').getByText('No motion')).toBeInTheDocument();
    expect(room('Living Room').getByText('Motion')).toBeInTheDocument();
    expect(room('Garage').getByText(/^921/)).toHaveTextContent('921 ppm');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('updates the pushed meter only', async () => {
    const hub = await renderConnected();
    await screen.findByText('845.62');

    act(() => {
      hub.connection.push(hubMessage('Energy', 'OFFICE', pushed, { energy: 912.85 }));
    });

    expect(room('Office').getByText('912.85')).toHaveAttribute('data-highlighted', 'true');
    expect(room('Kitchen').getByText('845.62')).not.toHaveAttribute('data-highlighted');
    expect(screen.getByText('Latest samples received at 14:30:02')).toBeInTheDocument();
  });

  it('keeps a push that arrived before older stored values', async () => {
    const hub = await renderLive([{ ...latest, delay: 100 }]);
    await settle(() => {
      hub.connection.connect();
    });

    act(() => {
      hub.connection.push(hubMessage('Energy', 'OFFICE', pushed, { energy: 912.85 }));
    });
    expect(room('Office').getByText('912.85')).toBeInTheDocument();
    expect(room('Kitchen').getByText('Waiting for the first sample')).toBeInTheDocument();

    expect(await room('Kitchen').findByText('845.62')).toBeInTheDocument();
    expect(room('Office').getByText('912.85')).not.toHaveAttribute('data-highlighted');
    expect(screen.getByText('Latest samples received at 14:30:02')).toBeInTheDocument();
  });

  it('replaces a push with newer stored values', async () => {
    const hub = await renderLive();
    act(() => {
      hub.connection.push(
        hubMessage('Energy', 'OFFICE', '2026-11-15T11:29:32Z', { energy: 912.85 }),
      );
    });

    await settle(() => {
      hub.connection.connect();
    });

    expect(await room('Office').findByText('603.17')).toBeInTheDocument();
    expect(screen.queryByText('912.85')).not.toBeInTheDocument();
  });

  it('ignores a push older than the stored value', async () => {
    const hub = await renderConnected();
    await screen.findByText('603.17');

    act(() => {
      hub.connection.push(
        hubMessage('Energy', 'OFFICE', '2026-11-15T11:29:32Z', { energy: 912.85 }),
      );
    });

    expect(room('Office').getByText('603.17')).toBeInTheDocument();
    expect(screen.queryByText('912.85')).not.toBeInTheDocument();
  });

  it('highlights a changed value but not the same value again', async () => {
    const hub = await renderConnected();
    await screen.findByText('845.62');

    act(() => {
      hub.connection.push(
        hubMessage('AirQuality', 'KITCHEN', pushed, { co2: 803, pm25: 30, humidity: 36 }),
      );
    });

    expect(room('Kitchen').getByText(/^803/)).not.toHaveAttribute('data-highlighted');
    expect(room('Kitchen').getByText(/^30/)).toHaveAttribute('data-highlighted', 'true');
  });

  it('rings the motion chip when motion changes', async () => {
    const hub = await renderConnected();
    await screen.findByText('845.62');

    act(() => {
      hub.connection.push(hubMessage('Motion', 'KITCHEN', pushed, { motionDetected: true }));
    });

    expect(room('Kitchen').getByText('Motion').parentElement).toHaveAttribute(
      'data-highlighted',
      'true',
    );
  });

  it('shows the warnings above the thresholds only', async () => {
    const rows = liveLatestRows(stored, {
      KITCHEN: { co2: 800, pm25: 35 },
      GARAGE: { co2: 801, pm25: 36 },
    });
    await renderConnected([liveLatestReadingsMock(rows)]);
    await screen.findByText('845.62');

    expect(room('Kitchen').queryByText(/above/)).not.toBeInTheDocument();
    expect(room('Garage').getByText('CO₂ above 800 ppm')).toBeInTheDocument();
    expect(room('Garage').getByText('PM2.5 above 35 µg/m³')).toBeInTheDocument();
  });

  it('dims the rooms while reconnecting and reloads the stored values afterwards', async () => {
    const reloaded = liveLatestReadingsMock(
      liveLatestRows('2026-11-15T11:30:17Z', { BEDROOM: { energy: 222.22 } }),
    );
    const hub = await renderConnected([latest, reloaded]);
    await screen.findByText('128.40');

    act(() => {
      hub.connection.drop();
    });
    expect(status()).toHaveTextContent('Reconnecting…');
    expect(screen.getByText('Values received up to 14:29:47')).toBeInTheDocument();

    act(() => {
      hub.connection.restore();
    });
    expect(status()).toHaveTextContent('Live');
    expect(await room('Bedroom').findByText('222.22')).toBeInTheDocument();
    expect(screen.getByText('Latest samples received at 14:30:17')).toBeInTheDocument();
  });

  it('keeps the values when reloading them after a reconnect fails', async () => {
    const hub = await renderConnected([latest, failing(latest)]);
    await screen.findByText('128.40');

    act(() => {
      hub.connection.drop();
    });
    act(() => {
      hub.connection.restore();
    });

    expect(await screen.findByText(/Couldn't load the latest stored values/)).toBeInTheDocument();
    expect(room('Bedroom').getByText('128.40')).toBeInTheDocument();
    expect(status()).toHaveTextContent('Live');
  });

  it('keeps the stored values and offers to reconnect when the first connection fails', async () => {
    const user = userEvent.setup();
    const reloaded = liveLatestReadingsMock(
      liveLatestRows('2026-11-15T11:30:17Z', { BEDROOM: { energy: 222.22 } }),
    );
    const hub = await renderLive([latest, reloaded]);

    await settle(() => {
      hub.connection.failStart();
    });

    expect(status()).toHaveTextContent('Offline');
    const alert = await screen.findByRole('alert');
    expect(
      within(alert).getByRole('heading', { name: "Live updates aren't available" }),
    ).toBeInTheDocument();
    await screen.findByText('128.40');
    expect(room('Bedroom').getByText('128.40')).toBeInTheDocument();
    expect(alert).toHaveTextContent("received at 14:29:47, and won't change");

    await user.click(within(alert).getByRole('button', { name: 'Reconnect' }));
    expect(status()).toHaveTextContent('Connecting…');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    await settle(() => {
      hub.connection.connect();
    });
    expect(status()).toHaveTextContent('Live');
    expect(await room('Bedroom').findByText('222.22')).toBeInTheDocument();
  });

  it('goes offline when the connection closes for good', async () => {
    const hub = await renderConnected();

    act(() => {
      hub.connection.close();
    });

    expect(status()).toHaveTextContent('Offline');
    expect(screen.getByRole('alert')).toHaveTextContent('Live updates aren');
  });

  it.each([
    ['2026-11-15T11:28:00Z', false],
    ['2026-11-15T11:27:59Z', true],
  ])('shows the stale notice for samples received at %s: %s', async (receivedAt, shown) => {
    await renderConnected([liveLatestReadingsMock(liveLatestRows(receivedAt))]);
    await screen.findByText('845.62');

    expect(screen.queryByText(/No new samples for/) !== null).toBe(shown);
  });

  it('does not show the stale notice while offline', async () => {
    const hub = await renderLive([liveLatestReadingsMock(liveLatestRows('2026-11-15T11:20:00Z'))]);
    await settle(() => {
      hub.connection.failStart();
    });
    await screen.findByText('845.62');

    expect(screen.queryByText(/No new samples for/)).not.toBeInTheDocument();
  });

  it('fills the rooms from pushes when the stored values fail, and retries them', async () => {
    const user = userEvent.setup();
    const hub = await renderLive([failing(latest), latest]);
    await settle(() => {
      hub.connection.connect();
    });

    await screen.findByText(
      "Couldn't load the latest stored values. Rooms fill in as new samples arrive.",
    );
    expect(room('Kitchen').getByText('Motion: waiting')).toBeInTheDocument();
    expect(room('Kitchen').getByText('Waiting for the first sample')).toBeInTheDocument();
    expect(
      room('Kitchen').getByText('Air quality: waiting for the first sample'),
    ).toBeInTheDocument();

    act(() => {
      hub.connection.push(hubMessage('Energy', 'KITCHEN', pushed, { energy: 912.85 }));
    });
    expect(room('Kitchen').getByText('912.85')).not.toHaveAttribute('data-highlighted');

    await user.click(screen.getByRole('button', { name: 'Retry' }));
    expect(await room('Office').findByText('603.17')).toBeInTheDocument();
    expect(room('Kitchen').getByText('912.85')).toBeInTheDocument();
    expect(screen.queryByText(/Couldn't load/)).not.toBeInTheDocument();
  });

  it('shows every room waiting when nothing is stored yet', async () => {
    await renderConnected([liveLatestReadingsMock([])]);

    expect(await screen.findAllByText('Motion: waiting')).toHaveLength(6);
    expect(screen.queryByText(/Latest samples received at/)).not.toBeInTheDocument();
  });

  it('links to the Overview for trends', async () => {
    await renderConnected();

    expect(
      screen.getByRole('link', { name: 'Trends and coverage are on the Overview' }),
    ).toHaveAttribute('href', '/');
  });
});
