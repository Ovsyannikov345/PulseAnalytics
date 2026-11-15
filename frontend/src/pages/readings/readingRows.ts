import type { TimeRange } from '../../domain/timeRanges';
import type { MeterLocation, MeterType } from '../../graphql/generated/enums';
import type { ReadingsTableQuery } from '../../graphql/generated/graphql';

export type ReadingValues =
  | { kind: 'airQuality'; co2: number; pm25: number; humidity: number }
  | { kind: 'energy'; energy: number }
  | { kind: 'motion'; motionDetected: boolean };

export interface ReadingRow {
  id: string;
  type: MeterType;
  location: MeterLocation;
  receivedAt: Date;
  values: ReadingValues;
}

/** The loaded pages of one filter, newest first. */
export interface ReadingsPageData {
  rows: ReadingRow[];
  hasNextPage: boolean;
  /** The range the rows were loaded for, for the empty state. */
  range: TimeRange;
}

type ReadingNode = NonNullable<
  NonNullable<ReadingsTableQuery['readings']>['edges']
>[number]['node'];

function toValues(node: ReadingNode): ReadingValues {
  switch (node.__typename) {
    case 'AirQualityReading':
      return { kind: 'airQuality', co2: node.co2, pm25: node.pm25, humidity: node.humidity };
    case 'EnergyReading':
      return { kind: 'energy', energy: node.energy };
    default:
      return { kind: 'motion', motionDetected: node.motionDetected };
  }
}

/** The pages Apollo merged so far (`relayStylePagination`); a missing connection has no rows. */
export function toReadingsPageData(
  readings: ReadingsTableQuery['readings'],
  range: TimeRange,
): ReadingsPageData {
  return {
    rows: (readings?.edges ?? []).map(({ node }) => ({
      id: node.id,
      type: node.type,
      location: node.location,
      receivedAt: new Date(node.receivedAt),
      values: toValues(node),
    })),
    hasNextPage: readings?.pageInfo.hasNextPage ?? false,
    range,
  };
}
