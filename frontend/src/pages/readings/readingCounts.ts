import type { MeterType } from '../../graphql/generated/enums';
import type { ReadingsMatchCountsQuery } from '../../graphql/generated/graphql';

export interface MatchCounts {
  total: number;
  /** One per selected type, in the filter's order; a type without readings counts 0. */
  byType: { type: MeterType; count: number }[];
}

/** The gateway leaves out groups without readings, so every selected type is filled in. */
export function toMatchCounts(
  rows: ReadingsMatchCountsQuery['readingCounts'],
  types: readonly MeterType[],
): MatchCounts {
  const byType = types.map((type) => ({
    type,
    count: rows.find((row) => row.type === type)?.count ?? 0,
  }));

  return { total: byType.reduce((sum, { count }) => sum + count, 0), byType };
}
