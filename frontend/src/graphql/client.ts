import { ApolloClient, HttpLink } from '@apollo/client';

import { createCache } from './cache';

/**
 * The app's Apollo client. `/graphql` is same-origin: the image's nginx and the Vite dev server
 * proxy it to the API gateway, so no gateway URL is built into the app.
 */
export function createApolloClient(): ApolloClient {
  return new ApolloClient({
    link: new HttpLink({ uri: '/graphql' }),
    cache: createCache(),
  });
}
