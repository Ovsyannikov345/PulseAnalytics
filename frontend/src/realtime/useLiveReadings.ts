import { use, useCallback, useEffect, useEffectEvent, useRef, useState } from 'react';

import { type LiveReadings, mergeReadings, noLiveReadings } from '../domain/liveReadings';
import { parseLiveReading } from './liveReadingMessage';
import type { ReadingsHubConnection } from './readingsHub';
import { ReadingsHubContext } from './ReadingsHubContext';

export type HubStatus = 'connecting' | 'live' | 'reconnecting' | 'offline';

interface UseLiveReadingsOptions {
  /** After a reconnect (automatic or `reconnect()`): pushes sent meanwhile were missed. */
  onReconnected: () => void;
}

export interface LiveReadingsState {
  status: HubStatus;
  /** The newest pushed reading per meter. */
  readings: LiveReadings;
  /** True once the first start has succeeded or failed; stays true. */
  settled: boolean;
  /** Starts the connection again after it went offline. */
  reconnect: () => void;
}

/**
 * The readings hub connection of one page: started on mount, stopped on unmount. The connection is
 * created in a zero-delay timer, so React StrictMode's immediate unmount cancels the first one
 * before it exists.
 */
export function useLiveReadings({ onReconnected }: UseLiveReadingsOptions): LiveReadingsState {
  const createConnection = use(ReadingsHubContext);
  const [status, setStatus] = useState<HubStatus>('connecting');
  const [readings, setReadings] = useState<LiveReadings>(noLiveReadings);
  const [settled, setSettled] = useState(false);
  const restartRef = useRef<(() => void) | null>(null);
  const notifyReconnected = useEffectEvent(onReconnected);

  useEffect(() => {
    let connection: ReadingsHubConnection | null = null;
    let disposed = false;

    const start = (current: ReadingsHubConnection, isFirst: boolean) => {
      current.start().then(
        () => {
          if (!disposed) {
            setStatus('live');
            setSettled(true);
            if (!isFirst) {
              notifyReconnected();
            }
          }
        },
        () => {
          if (!disposed) {
            setStatus('offline');
            setSettled(true);
          }
        },
      );
    };

    const timer = setTimeout(() => {
      const current = createConnection();
      connection = current;

      current.onReadingStored((message) => {
        const reading = parseLiveReading(message);
        if (reading && !disposed) {
          setReadings((kept) => mergeReadings(kept, [reading]));
        }
      });
      current.onReconnecting(() => {
        if (!disposed) {
          setStatus('reconnecting');
        }
      });
      current.onReconnected(() => {
        if (!disposed) {
          setStatus('live');
          notifyReconnected();
        }
      });
      current.onClose(() => {
        if (!disposed) {
          setStatus('offline');
        }
      });

      restartRef.current = () => {
        setStatus('connecting');
        start(current, false);
      };
      start(current, true);
    }, 0);

    return () => {
      disposed = true;
      clearTimeout(timer);
      restartRef.current = null;
      void connection?.stop();
    };
  }, [createConnection]);

  const reconnect = useCallback(() => {
    restartRef.current?.();
  }, []);

  return { status, readings, settled, reconnect };
}
