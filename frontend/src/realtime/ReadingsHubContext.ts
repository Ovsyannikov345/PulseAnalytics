import { createContext } from 'react';

import { createReadingsHubConnection, type ReadingsHubConnectionFactory } from './readingsHub';

/** How the Live page creates its hub connection; tests provide a fake connection here. */
export const ReadingsHubContext = createContext<ReadingsHubConnectionFactory>(
  createReadingsHubConnection,
);
