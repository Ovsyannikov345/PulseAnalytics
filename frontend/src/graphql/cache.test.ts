import { gql } from '@apollo/client';
import { describe, expect, it } from 'vitest';

import { createCache } from './cache';

const readingFields = gql`
  fragment ReadingFields on MeterReading {
    id
    type
    location
    receivedAt
    ... on EnergyReading {
      energy
    }
  }
`;

const readingsQuery = gql`
  query Readings($filter: ReadingFilterInput, $first: Int, $after: String) {
    readings(filter: $filter, first: $first, after: $after) {
      edges {
        cursor
        node {
          ...ReadingFields
        }
      }
      pageInfo {
        hasNextPage
        endCursor
      }
    }
  }
  ${readingFields}
`;

interface Variables {
  filter?: { types?: string[]; locations?: string[] };
  first?: number;
  after?: string | null;
}

interface ReadingsData {
  readings: {
    edges: { cursor: string; node: { id: string } }[];
    pageInfo: { hasNextPage: boolean; endCursor: string | null };
  };
}

function energyReading(id: string) {
  return {
    __typename: 'EnergyReading',
    id,
    type: 'ENERGY',
    location: 'KITCHEN',
    receivedAt: '2026-11-14T12:00:00Z',
    energy: 1.5,
  };
}

function page(ids: string[], hasNextPage: boolean) {
  return {
    readings: {
      __typename: 'MeterReadingConnection',
      edges: ids.map((id) => ({
        __typename: 'MeterReadingEdge',
        cursor: `cursor-${id}`,
        node: energyReading(id),
      })),
      pageInfo: {
        __typename: 'PageInfo',
        hasNextPage,
        endCursor: ids.length > 0 ? `cursor-${ids.at(-1) ?? ''}` : null,
      },
    },
  };
}

const kitchen = { locations: ['KITCHEN'] };
const garage = { locations: ['GARAGE'] };

function writePage(
  cache: ReturnType<typeof createCache>,
  variables: Variables,
  data: ReturnType<typeof page>,
) {
  cache.writeQuery({ query: readingsQuery, variables, data });
}

function readIds(cache: ReturnType<typeof createCache>, variables: Variables) {
  const data = cache.readQuery<ReadingsData>({ query: readingsQuery, variables });
  return data?.readings.edges.map((edge) => edge.node.id);
}

describe('createCache', () => {
  it('appends the next page of the same filter after the loaded ones', () => {
    const cache = createCache();

    writePage(cache, { filter: kitchen, first: 2 }, page(['a', 'b'], true));
    writePage(cache, { filter: kitchen, first: 2, after: 'cursor-b' }, page(['c', 'd'], false));

    const data = cache.readQuery<ReadingsData>({
      query: readingsQuery,
      variables: { filter: kitchen, first: 2 },
    });
    expect(data?.readings.edges.map((edge) => edge.node.id)).toEqual(['a', 'b', 'c', 'd']);
    expect(data?.readings.pageInfo).toMatchObject({ hasNextPage: false, endCursor: 'cursor-d' });
  });

  it('keeps a separate list per filter', () => {
    const cache = createCache();

    writePage(cache, { filter: kitchen, first: 2 }, page(['a', 'b'], true));
    writePage(cache, { filter: garage, first: 2 }, page(['x'], false));

    expect(readIds(cache, { filter: kitchen, first: 2 })).toEqual(['a', 'b']);
    expect(readIds(cache, { filter: garage, first: 2 })).toEqual(['x']);
  });

  it('starts the list again when the first page is fetched again', () => {
    const cache = createCache();

    writePage(cache, { filter: kitchen, first: 2 }, page(['a', 'b'], true));
    writePage(cache, { filter: kitchen, first: 2, after: 'cursor-b' }, page(['c', 'd'], false));
    writePage(cache, { filter: kitchen, first: 2 }, page(['new', 'a'], true));

    expect(readIds(cache, { filter: kitchen, first: 2 })).toEqual(['new', 'a']);
  });

  it('reads fragments on the MeterReading interface back from normalized readings', () => {
    const cache = createCache();

    writePage(cache, { filter: kitchen, first: 1 }, page(['a'], false));

    const reading = cache.readFragment({
      id: cache.identify({ __typename: 'EnergyReading', id: 'a' }),
      fragment: readingFields,
      fragmentName: 'ReadingFields',
    });
    expect(reading).toMatchObject({ id: 'a', type: 'ENERGY', energy: 1.5 });
  });
});
