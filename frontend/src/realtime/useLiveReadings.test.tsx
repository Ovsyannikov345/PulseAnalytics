import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { meterKey } from '../domain/liveReadings';
import { FakeReadingsHub } from '../test/fakeReadingsHub';
import { ReadingsHubContext } from './ReadingsHubContext';
import { useLiveReadings } from './useLiveReadings';

const energyMessage = {
  type: 'Energy',
  id: '019a91b2-4c6e-7d13-9a58-3e7f2b6c1d02',
  location: 'Office',
  receivedAt: '2026-11-17T10:15:30.123+00:00',
  energy: 845.62,
};

async function renderLiveReadings({ strictMode = false } = {}) {
  const hub = new FakeReadingsHub();
  const onReconnected = vi.fn<() => void>();
  const wrapper = ({ children }: { children: ReactNode }) => (
    <ReadingsHubContext value={hub.factory}>{children}</ReadingsHubContext>
  );

  const result = renderHook(() => useLiveReadings({ onReconnected }), {
    wrapper,
    reactStrictMode: strictMode,
  });
  await waitFor(() => {
    expect(hub.connections).toHaveLength(1);
  });

  return { ...result, hub, onReconnected };
}

describe('useLiveReadings', () => {
  it('connects once the first start succeeds', async () => {
    const { result, hub } = await renderLiveReadings();

    expect(result.current.status).toBe('connecting');
    expect(result.current.settled).toBe(false);
    expect(hub.connection.startCount).toBe(1);

    await act(async () => {
      hub.connection.connect();
      await Promise.resolve();
    });

    expect(result.current.status).toBe('live');
    expect(result.current.settled).toBe(true);
  });

  it('goes offline when the first start fails, and reconnects on request', async () => {
    const { result, hub, onReconnected } = await renderLiveReadings();

    await act(async () => {
      hub.connection.failStart();
      await Promise.resolve();
    });
    expect(result.current.status).toBe('offline');
    expect(result.current.settled).toBe(true);

    act(() => {
      result.current.reconnect();
    });
    expect(result.current.status).toBe('connecting');
    expect(hub.connection.startCount).toBe(2);

    await act(async () => {
      hub.connection.connect();
      await Promise.resolve();
    });
    expect(result.current.status).toBe('live');
    expect(onReconnected).toHaveBeenCalledOnce();
    expect(hub.connections).toHaveLength(1);
  });

  it('stays offline when a requested reconnect fails', async () => {
    const { result, hub, onReconnected } = await renderLiveReadings();
    await act(async () => {
      hub.connection.failStart();
      await Promise.resolve();
    });

    act(() => {
      result.current.reconnect();
    });
    await act(async () => {
      hub.connection.failStart();
      await Promise.resolve();
    });

    expect(result.current.status).toBe('offline');
    expect(onReconnected).not.toHaveBeenCalled();
  });

  it('merges valid pushes and drops invalid ones', async () => {
    const { result, hub } = await renderLiveReadings();
    await act(async () => {
      hub.connection.connect();
      await Promise.resolve();
    });

    act(() => {
      hub.connection.push(energyMessage);
      hub.connection.push({ ...energyMessage, id: 'other', location: 'Attic' });
      hub.connection.push('not a reading');
    });

    expect([...result.current.readings.values()]).toEqual([
      {
        type: 'ENERGY',
        id: energyMessage.id,
        location: 'OFFICE',
        receivedAt: energyMessage.receivedAt,
        energy: 845.62,
      },
    ]);
  });

  it('keeps the same readings for a redelivered push', async () => {
    const { result, hub } = await renderLiveReadings();
    act(() => {
      hub.connection.push(energyMessage);
    });
    const readings = result.current.readings;

    act(() => {
      hub.connection.push({ ...energyMessage });
    });

    expect(result.current.readings).toBe(readings);
    expect(result.current.readings.get(meterKey('ENERGY', 'OFFICE'))?.id).toBe(energyMessage.id);
  });

  it('reports reconnecting and calls back once reconnected', async () => {
    const { result, hub, onReconnected } = await renderLiveReadings();
    await act(async () => {
      hub.connection.connect();
      await Promise.resolve();
    });

    act(() => {
      hub.connection.drop();
    });
    expect(result.current.status).toBe('reconnecting');
    expect(onReconnected).not.toHaveBeenCalled();

    act(() => {
      hub.connection.restore();
    });
    expect(result.current.status).toBe('live');
    expect(onReconnected).toHaveBeenCalledOnce();
  });

  it('goes offline when the connection closes for good', async () => {
    const { result, hub } = await renderLiveReadings();
    await act(async () => {
      hub.connection.connect();
      await Promise.resolve();
    });

    act(() => {
      hub.connection.close();
    });

    expect(result.current.status).toBe('offline');
  });

  it('stops the connection on unmount and ignores it afterwards', async () => {
    const { result, hub, unmount, onReconnected } = await renderLiveReadings();
    const connection = hub.connection;

    unmount();
    expect(connection.stopCount).toBe(1);

    await act(async () => {
      connection.connect();
      connection.push(energyMessage);
      connection.drop();
      connection.restore();
      connection.close();
      await Promise.resolve();
    });
    expect(result.current.status).toBe('connecting');
    expect(result.current.readings.size).toBe(0);
    expect(onReconnected).not.toHaveBeenCalled();
  });

  it('ignores a first start that fails after unmount', async () => {
    const { result, hub, unmount } = await renderLiveReadings();
    const connection = hub.connection;

    unmount();
    await act(async () => {
      connection.failStart();
      await Promise.resolve();
    });

    expect(result.current.status).toBe('connecting');
    expect(result.current.settled).toBe(false);
  });

  it('does nothing when asked to reconnect before a connection exists', () => {
    const hub = new FakeReadingsHub();
    const { result, unmount } = renderHook(() => useLiveReadings({ onReconnected: vi.fn() }), {
      wrapper: ({ children }: { children: ReactNode }) => (
        <ReadingsHubContext value={hub.factory}>{children}</ReadingsHubContext>
      ),
    });

    act(() => {
      result.current.reconnect();
    });
    unmount();

    expect(hub.connections).toHaveLength(0);
  });

  it('creates and starts one connection under StrictMode', async () => {
    const { hub } = await renderLiveReadings({ strictMode: true });

    await new Promise((resolve) => setTimeout(resolve, 10));

    expect(hub.connections).toHaveLength(1);
    expect(hub.connection.startCount).toBe(1);
    expect(hub.connection.stopCount).toBe(0);
  });
});
