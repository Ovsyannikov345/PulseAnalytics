import type { RouteObject } from 'react-router';

import { AnalyticsPage } from '../pages/analytics/AnalyticsPage';
import { NotFoundPage } from '../pages/not-found/NotFoundPage';
import { OverviewPage } from '../pages/overview/OverviewPage';
import { ReadingsPage } from '../pages/readings/ReadingsPage';
import { AppLayout } from './AppLayout';

export const routes: RouteObject[] = [
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <OverviewPage /> },
      { path: 'analytics', element: <AnalyticsPage /> },
      { path: 'readings', element: <ReadingsPage /> },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
