import { CombinedGraphQLErrors } from '@apollo/client/errors';

/**
 * How a widget shows a failed query:
 * - `invalidInput`: the gateway rejected the filter; the message goes next to the filter.
 * - `unavailable`: anything else (network failure, HTTP error, `INTERNAL_ERROR`, unknown codes);
 *   the widget shows its error with Retry.
 */
export type QueryErrorKind = 'invalidInput' | 'unavailable';

/** `BAD_REQUEST` from the gateway's validation, `HC0051` from HotChocolate's paging (page size over 100). */
const invalidInputCodes: ReadonlySet<string> = new Set(['BAD_REQUEST', 'HC0051']);

/** Shown for `invalidInput`. Server messages are never shown, so the texts stay the client's. */
export const INVALID_FILTER_MESSAGE =
  "The gateway didn't accept these filters. Check the time range and try again.";

export function getQueryErrorKind(error: unknown): QueryErrorKind {
  if (
    CombinedGraphQLErrors.is(error) &&
    error.errors.some((graphQLError) => {
      const code = graphQLError.extensions?.code;
      return typeof code === 'string' && invalidInputCodes.has(code);
    })
  ) {
    return 'invalidInput';
  }

  return 'unavailable';
}
