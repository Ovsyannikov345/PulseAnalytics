import { ApolloClient, HttpLink } from '@apollo/client';

import { createCache } from './cache';

/** The app's Apollo client. */
export function createApolloClient(): ApolloClient {
  return new ApolloClient({
    link: new HttpLink({ uri: '/graphql' }),
    cache: createCache(),
  });
}
