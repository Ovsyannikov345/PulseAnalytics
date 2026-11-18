import type { ReadingsHubConnection } from '../realtime/readingsHub';

type Handler<T extends unknown[] = []> = (...args: T) => void;

/** A hub connection the test drives: it connects, fails, pushes and drops only when told to. */
export class FakeReadingsHubConnection implements ReadingsHubConnection {
  startCount = 0;
  stopCount = 0;
  private pendingStart: { resolve: () => void; reject: (error: Error) => void } | null = null;
  private readonly readingHandlers: Handler<[unknown]>[] = [];
  private readonly reconnectingHandlers: Handler[] = [];
  private readonly reconnectedHandlers: Handler[] = [];
  private readonly closeHandlers: Handler[] = [];

  start = () => {
    this.startCount += 1;
    return new Promise<void>((resolve, reject) => {
      this.pendingStart = { resolve, reject };
    });
  };

  stop = () => {
    this.stopCount += 1;
    return Promise.resolve();
  };

  onReadingStored = (handler: Handler<[unknown]>) => {
    this.readingHandlers.push(handler);
  };

  onReconnecting = (handler: Handler) => {
    this.reconnectingHandlers.push(handler);
  };

  onReconnected = (handler: Handler) => {
    this.reconnectedHandlers.push(handler);
  };

  onClose = (handler: Handler) => {
    this.closeHandlers.push(handler);
  };

  /** Completes the pending `start()`. */
  connect() {
    this.takePendingStart().resolve();
  }

  /** Fails the pending `start()`, as when the service doesn't answer. */
  failStart() {
    this.takePendingStart().reject(new Error('Failed to complete negotiation with the server'));
  }

  push(message: unknown) {
    this.readingHandlers.forEach((handler) => {
      handler(message);
    });
  }

  drop() {
    this.reconnectingHandlers.forEach((handler) => {
      handler();
    });
  }

  restore() {
    this.reconnectedHandlers.forEach((handler) => {
      handler();
    });
  }

  close() {
    this.closeHandlers.forEach((handler) => {
      handler();
    });
  }

  private takePendingStart() {
    const pending = this.pendingStart;
    if (!pending) {
      throw new Error('start() is not pending');
    }

    this.pendingStart = null;
    return pending;
  }
}

/** A connection factory that records every connection it creates. */
export class FakeReadingsHub {
  readonly connections: FakeReadingsHubConnection[] = [];

  readonly factory = () => {
    const connection = new FakeReadingsHubConnection();
    this.connections.push(connection);
    return connection;
  };

  get connection(): FakeReadingsHubConnection {
    const connection = this.connections.at(-1);
    if (!connection) {
      throw new Error('No connection was created');
    }

    return connection;
  }
}
