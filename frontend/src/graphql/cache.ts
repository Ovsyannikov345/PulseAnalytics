import { InMemoryCache } from '@apollo/client';
import { relayStylePagination } from '@apollo/client/utilities';

import introspection from './generated/possibleTypes';

/**
 * The Apollo cache with the app's policies; tests pass a fresh one to their provider.
 * - `possibleTypes` lets fragments on the `MeterReading` interface match its implementations.
 * - `readings` keeps one list per filter and appends the pages fetched with `after`.
 *   The policy works on `edges`, so the readings query selects `edges { cursor node }`, not `nodes`.
 */
export function createCache(): InMemoryCache {
  return new InMemoryCache({
    possibleTypes: introspection.possibleTypes,
    typePolicies: {
      Query: {
        fields: {
          readings: relayStylePagination(['filter']),
        },
      },
    },
  });
}
