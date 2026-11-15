import { InMemoryCache } from '@apollo/client';
import { relayStylePagination } from '@apollo/client/utilities';

import introspection from './generated/possibleTypes';

/** The Apollo cache with the app's policies */
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
