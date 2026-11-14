import '@fontsource-variable/inter';
import './styles/tokens.css';
import './styles/global.css';

import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter } from 'react-router';
import { RouterProvider } from 'react-router/dom';

import { routes } from './app/routes';

const router = createBrowserRouter(routes);

const rootElement = document.getElementById('root');

if (!rootElement) {
  throw new Error('The #root element is missing from index.html.');
}

createRoot(rootElement).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
);
