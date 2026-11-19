import { HubConnectionState } from '@microsoft/signalr';
import { describe, expect, it, vi } from 'vitest';

import {
  adaptHubConnection,
  buildReadingsHubConnection,
  createReadingsHubConnection,
  READING_STORED_METHOD,
  reconnectPolicy,
} from './readingsHub';

describe('reconnectPolicy', () => {
  it.each([
    [0, 0],
    [1, 2_000],
    [2, 5_000],
    [3, 10_000],
    [4, 30_000],
    [5, 30_000],
    [100, 30_000],
  ])('waits after %i failed retries %i ms', (previousRetryCount, delayMs) => {
    const delay = reconnectPolicy.nextRetryDelayInMilliseconds({
      previousRetryCount,
      elapsedMilliseconds: 0,
      retryReason: new Error('Connection lost'),
    });

    expect(delay).toBe(delayMs);
  });
});

describe('buildReadingsHubConnection', () => {
  it('builds a stopped connection to the same-origin hub', () => {
    const connection = buildReadingsHubConnection();

    expect(connection.state).toBe(HubConnectionState.Disconnected);
    expect(new URL(connection.baseUrl).pathname).toBe('/hubs/readings');
    expect(new URL(connection.baseUrl).origin).toBe(window.location.origin);
  });

  it('creates a connection for the page', () => {
    const connection = createReadingsHubConnection();

    expect(connection.start).toBeTypeOf('function');
  });
});

describe('adaptHubConnection', () => {
  function fakeHubConnection() {
    return {
      start: vi.fn(() => Promise.resolve()),
      stop: vi.fn(() => Promise.resolve()),
      on: vi.fn<(method: string, handler: (...args: unknown[]) => void) => void>(),
      onreconnecting: vi.fn<(handler: (error?: Error) => void) => void>(),
      onreconnected: vi.fn<(handler: (connectionId?: string) => void) => void>(),
      onclose: vi.fn<(handler: (error?: Error) => void) => void>(),
    };
  }

  it('starts and stops the connection', async () => {
    const hub = fakeHubConnection();
    const connection = adaptHubConnection(hub);

    await connection.start();
    await connection.stop();

    expect(hub.start).toHaveBeenCalledOnce();
    expect(hub.stop).toHaveBeenCalledOnce();
  });

  it('passes ReadingStored messages on unparsed', () => {
    const hub = fakeHubConnection();
    const handler = vi.fn();

    adaptHubConnection(hub).onReadingStored(handler);
    const [method, registered] = hub.on.mock.calls[0] ?? [];
    registered?.({ type: 'Energy' });

    expect(method).toBe(READING_STORED_METHOD);
    expect(handler).toHaveBeenCalledWith({ type: 'Energy' });
  });

  it('passes the connection events on without their arguments', () => {
    const hub = fakeHubConnection();
    const connection = adaptHubConnection(hub);
    const onReconnecting = vi.fn();
    const onReconnected = vi.fn();
    const onClose = vi.fn();

    connection.onReconnecting(onReconnecting);
    connection.onReconnected(onReconnected);
    connection.onClose(onClose);
    hub.onreconnecting.mock.calls[0]?.[0](new Error('Connection lost'));
    hub.onreconnected.mock.calls[0]?.[0]('connection-id');
    hub.onclose.mock.calls[0]?.[0]();

    expect(onReconnecting).toHaveBeenCalledWith();
    expect(onReconnected).toHaveBeenCalledWith();
    expect(onClose).toHaveBeenCalledWith();
  });
});
