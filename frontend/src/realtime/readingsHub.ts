import {
  type HubConnection,
  HubConnectionBuilder,
  type IRetryPolicy,
  LogLevel,
} from '@microsoft/signalr';

/** The notification service's hub, same origin through nginx (compose) or the Vite proxy (dev). */
export const READINGS_HUB_PATH = '/hubs/readings';

/** The hub's only client method; the hub has no server methods. */
export const READING_STORED_METHOD = 'ReadingStored';

const RECONNECT_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000] as const;

/** Retries after 0, 2, 5, 10 and 30 s, then every 30 s for as long as the page is open. */
export const reconnectPolicy: IRetryPolicy = {
  nextRetryDelayInMilliseconds: ({ previousRetryCount }) =>
    RECONNECT_DELAYS_MS[previousRetryCount] ?? 30_000,
};

/** The part of a hub connection the Live page uses; tests replace it with a fake. */
export interface ReadingsHubConnection {
  start: () => Promise<void>;
  stop: () => Promise<void>;
  /** Messages are untrusted: they reach the handler unparsed. */
  onReadingStored: (handler: (message: unknown) => void) => void;
  onReconnecting: (handler: () => void) => void;
  onReconnected: (handler: () => void) => void;
  /** After `stop()`, a server close or when automatic reconnecting gives up. */
  onClose: (handler: () => void) => void;
}

export type ReadingsHubConnectionFactory = () => ReadingsHubConnection;

export function buildReadingsHubConnection(): HubConnection {
  // Absolute, so the URL doesn't depend on how the client detects a browser.
  const url = new URL(READINGS_HUB_PATH, window.location.origin).href;

  return new HubConnectionBuilder()
    .withUrl(url)
    .withAutomaticReconnect(reconnectPolicy)
    .configureLogging(LogLevel.Warning)
    .build();
}

type HubConnectionLike = Pick<
  HubConnection,
  'start' | 'stop' | 'on' | 'onreconnecting' | 'onreconnected' | 'onclose'
>;

export function adaptHubConnection(connection: HubConnectionLike): ReadingsHubConnection {
  return {
    start: () => connection.start(),
    stop: () => connection.stop(),
    onReadingStored: (handler) => {
      connection.on(READING_STORED_METHOD, (message: unknown) => {
        handler(message);
      });
    },
    onReconnecting: (handler) => {
      connection.onreconnecting(() => {
        handler();
      });
    },
    onReconnected: (handler) => {
      connection.onreconnected(() => {
        handler();
      });
    },
    onClose: (handler) => {
      connection.onclose(() => {
        handler();
      });
    },
  };
}

export const createReadingsHubConnection: ReadingsHubConnectionFactory = () =>
  adaptHubConnection(buildReadingsHubConnection());
