import { gql } from '@apollo/client';
import { CombinedGraphQLErrors, ServerError } from '@apollo/client/errors';
import { describe, expect, it, vi } from 'vitest';

import { createApolloClient } from './client';
import { getQueryErrorKind } from './errors';

const query = gql`
  query TotalEnergy($filter: StatisticsFilterInput!) {
    energyStatistics(filter: $filter, groupBy: []) {
      samples
    }
  }
`;

const graphQLResponse = 'application/graphql-response+json; charset=utf-8';

function stubFetch(status: number, contentType: string, body: unknown) {
  const text = typeof body === 'string' ? body : JSON.stringify(body);
  const fetchMock = vi.fn<typeof fetch>(() =>
    Promise.resolve(new Response(text, { status, headers: { 'Content-Type': contentType } })),
  );
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

/** Runs the query through the app's real client (HttpLink included) and returns its error. */
async function queryError(): Promise<unknown> {
  const client = createApolloClient();
  const variables = { filter: { from: '2026-11-13T00:00:00Z', to: '2026-11-14T00:00:00Z' } };

  return client.query({ query, variables }).then(
    () => {
      throw new Error('The query was expected to fail.');
    },
    (error: unknown) => error,
  );
}

function graphQLError(code: string | undefined) {
  return {
    message: 'Some server message.',
    path: ['energyStatistics'],
    ...(code === undefined ? {} : { extensions: { code } }),
  };
}

// The responses are the gateway's (directly and through the image's nginx), recorded 2026-11-14.
describe('getQueryErrorKind with the app client', () => {
  it('posts the query to the same-origin /graphql endpoint', async () => {
    const fetchMock = stubFetch(200, graphQLResponse, {
      errors: [graphQLError('BAD_REQUEST')],
      data: null,
    });

    await queryError();

    expect(fetchMock).toHaveBeenCalledOnce();
    const [url, init] = fetchMock.mock.calls[0] ?? [];
    expect(url).toBe('/graphql');
    expect(init?.method).toBe('POST');
  });

  it.each([
    ['BAD_REQUEST', 'gateway validation, e.g. a range over 31 days'],
    ['HC0051', 'page size over 100'],
  ])('reports %s (%s) as invalid input', async (code) => {
    stubFetch(200, graphQLResponse, { errors: [graphQLError(code)], data: null });

    const error = await queryError();

    expect(CombinedGraphQLErrors.is(error)).toBe(true);
    expect(getQueryErrorKind(error)).toBe('invalidInput');
  });

  it('reports invalid input when one of several errors is a bad request', async () => {
    stubFetch(200, graphQLResponse, {
      errors: [graphQLError('INTERNAL_ERROR'), graphQLError('BAD_REQUEST')],
      data: null,
    });

    expect(getQueryErrorKind(await queryError())).toBe('invalidInput');
  });

  it('reports INTERNAL_ERROR (e.g. the database is down) as unavailable', async () => {
    stubFetch(200, graphQLResponse, { errors: [graphQLError('INTERNAL_ERROR')], data: null });

    expect(getQueryErrorKind(await queryError())).toBe('unavailable');
  });

  it('reports an error without a code as unavailable', async () => {
    stubFetch(200, graphQLResponse, { errors: [graphQLError(undefined)], data: null });

    expect(getQueryErrorKind(await queryError())).toBe('unavailable');
  });

  it('reports an HTTP 500 with GraphQL errors (execution timeout HC0045) as unavailable', async () => {
    stubFetch(500, graphQLResponse, { errors: [graphQLError('HC0045')] });

    const error = await queryError();

    // A GraphQL response body is parsed whatever the status, so this is not a ServerError.
    expect(CombinedGraphQLErrors.is(error)).toBe(true);
    expect(getQueryErrorKind(error)).toBe('unavailable');
  });

  it("reports nginx's 502 page (gateway down) as unavailable", async () => {
    stubFetch(502, 'text/html', '<html><head><title>502 Bad Gateway</title></head></html>');

    const error = await queryError();

    expect(ServerError.is(error)).toBe(true);
    expect(getQueryErrorKind(error)).toBe('unavailable');
  });

  it('reports a failed request (connection refused) as unavailable', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>(() => Promise.reject(new TypeError('Failed to fetch'))),
    );

    const error = await queryError();

    expect(error).toBeInstanceOf(TypeError);
    expect(getQueryErrorKind(error)).toBe('unavailable');
  });
});

describe('getQueryErrorKind with other values', () => {
  it.each([
    ['a plain error', new Error('Something failed.')],
    ['null', null],
    [
      'a GraphQL error with a non-string code',
      new CombinedGraphQLErrors({ errors: [{ message: 'x', extensions: { code: 400 } }] }),
    ],
  ])('reports %s as unavailable', (_, error) => {
    expect(getQueryErrorKind(error)).toBe('unavailable');
  });
});
