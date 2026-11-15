import '@fontsource-variable/inter';
import './styles/tokens.css';
import './styles/global.css';

import { ApolloProvider } from '@apollo/client/react';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';

import { routes } from './app/routes';
import { createApolloClient } from './graphql/client';

const apolloClient = createApolloClient();
const router = createBrowserRouter(routes);

const rootElement = document.getElementById('root');

if (!rootElement) {
  throw new Error('The #root element is missing from index.html.');
}

createRoot(rootElement).render(
  <StrictMode>
    <ApolloProvider client={apolloClient}>
      <RouterProvider router={router} />
    </ApolloProvider>
  </StrictMode>,
);
