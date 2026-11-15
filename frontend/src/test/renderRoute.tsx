import type { MockLink } from '@apollo/client/testing';
import { MockedProvider } from '@apollo/client/testing/react';
import { render } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';

import { routes } from '../app/routes';
import { createCache } from '../graphql/cache';

interface RenderRouteOptions {
  /** The GraphQL responses; without them every query fails quietly. */
  mocks?: readonly MockLink.MockedResponse[];
}

/** Renders the whole app at `path` with an in-memory router; returns the router to inspect its location. */
export function renderRoute(path: string, { mocks = [] }: RenderRouteOptions = {}) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <MockedProvider mocks={mocks} cache={createCache()} showWarnings={mocks.length > 0}>
      <RouterProvider router={router} />
    </MockedProvider>,
  );
  return router;
}
