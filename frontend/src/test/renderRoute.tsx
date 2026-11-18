import type { MockLink } from '@apollo/client/testing';
import { MockedProvider } from '@apollo/client/testing/react';
import { render } from '@testing-library/react';
import { createMemoryRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';

import { routes } from '../app/routes';
import { createCache } from '../graphql/cache';
import type { ReadingsHubConnectionFactory } from '../realtime/readingsHub';
import { ReadingsHubContext } from '../realtime/ReadingsHubContext';
import { FakeReadingsHub } from './fakeReadingsHub';

interface RenderRouteOptions {
  mocks?: readonly MockLink.MockedResponse[];
  /** The Live page's hub connection; by default a fake that never connects. */
  hubConnectionFactory?: ReadingsHubConnectionFactory;
}

/** Renders the whole app at `path` with an in-memory router; returns the router to inspect its location. */
export function renderRoute(
  path: string,
  { mocks = [], hubConnectionFactory = new FakeReadingsHub().factory }: RenderRouteOptions = {},
) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(
    <MockedProvider mocks={mocks} cache={createCache()} showWarnings={mocks.length > 0}>
      <ReadingsHubContext value={hubConnectionFactory}>
        <RouterProvider router={router} />
      </ReadingsHubContext>
    </MockedProvider>,
  );
  return router;
}
