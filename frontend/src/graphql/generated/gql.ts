/* eslint-disable */
import * as types from './graphql';
import type { TypedDocumentNode as DocumentNode } from '@graphql-typed-document-node/core';

/**
 * Map of all GraphQL operations in the project.
 *
 * This map has several performance disadvantages:
 * 1. It is not tree-shakeable, so it will include all operations in the project.
 * 2. It is not minifiable, so the string of a GraphQL query will be multiple times inside the bundle.
 * 3. It does not support dead code elimination, so it will add unused operations.
 *
 * Therefore it is highly recommended to use the babel or swc plugin for production.
 * Learn more about it here: https://the-guild.dev/graphql/codegen/plugins/presets/preset-client#reducing-bundle-size
 */
type Documents = {
  'query AnalyticsAirQuality($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  airQualityStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n    co2 {\n      average\n      minimum\n      maximum\n    }\n    pm25 {\n      average\n      minimum\n      maximum\n    }\n    humidity {\n      average\n      minimum\n      maximum\n    }\n  }\n}': typeof types.AnalyticsAirQualityDocument;
  'query AnalyticsAirQualitySummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  airQualityStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    co2 {\n      average\n    }\n    pm25 {\n      average\n    }\n    humidity {\n      average\n    }\n  }\n}': typeof types.AnalyticsAirQualitySummaryDocument;
  'query AnalyticsEnergy($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    samples\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}': typeof types.AnalyticsEnergyDocument;
  'query AnalyticsEnergySummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  energyStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}': typeof types.AnalyticsEnergySummaryDocument;
  'query AnalyticsMotionActivity($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  motionStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    samples\n    motionSamples\n    activityRatio\n  }\n}': typeof types.AnalyticsMotionActivityDocument;
  'query AnalyticsMotionSummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  motionStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    samples\n    activityRatio\n  }\n}': typeof types.AnalyticsMotionSummaryDocument;
  'query OverviewEnergySparklines($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    energy {\n      average\n    }\n  }\n}': typeof types.OverviewEnergySparklinesDocument;
  'query OverviewEnergyTrend($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}': typeof types.OverviewEnergyTrendDocument;
  'query OverviewIngestionCoverage($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  motionStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n  }\n}': typeof types.OverviewIngestionCoverageDocument;
  'query OverviewLatestReadings {\n  latestReadings {\n    location\n    receivedAt\n    ... on EnergyReading {\n      energy\n    }\n    ... on AirQualityReading {\n      co2\n      pm25\n      humidity\n    }\n    ... on MotionReading {\n      motionDetected\n    }\n  }\n}': typeof types.OverviewLatestReadingsDocument;
  'query OverviewSampleCount($from: DateTime!, $to: DateTime!) {\n  readingCounts(filter: {from: $from, to: $to}, groupBy: []) {\n    count\n  }\n}': typeof types.OverviewSampleCountDocument;
};
const documents: Documents = {
  'query AnalyticsAirQuality($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  airQualityStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n    co2 {\n      average\n      minimum\n      maximum\n    }\n    pm25 {\n      average\n      minimum\n      maximum\n    }\n    humidity {\n      average\n      minimum\n      maximum\n    }\n  }\n}':
    types.AnalyticsAirQualityDocument,
  'query AnalyticsAirQualitySummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  airQualityStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    co2 {\n      average\n    }\n    pm25 {\n      average\n    }\n    humidity {\n      average\n    }\n  }\n}':
    types.AnalyticsAirQualitySummaryDocument,
  'query AnalyticsEnergy($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    samples\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}':
    types.AnalyticsEnergyDocument,
  'query AnalyticsEnergySummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  energyStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}':
    types.AnalyticsEnergySummaryDocument,
  'query AnalyticsMotionActivity($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  motionStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    samples\n    motionSamples\n    activityRatio\n  }\n}':
    types.AnalyticsMotionActivityDocument,
  'query AnalyticsMotionSummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  motionStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    samples\n    activityRatio\n  }\n}':
    types.AnalyticsMotionSummaryDocument,
  'query OverviewEnergySparklines($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    energy {\n      average\n    }\n  }\n}':
    types.OverviewEnergySparklinesDocument,
  'query OverviewEnergyTrend($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}':
    types.OverviewEnergyTrendDocument,
  'query OverviewIngestionCoverage($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  motionStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n  }\n}':
    types.OverviewIngestionCoverageDocument,
  'query OverviewLatestReadings {\n  latestReadings {\n    location\n    receivedAt\n    ... on EnergyReading {\n      energy\n    }\n    ... on AirQualityReading {\n      co2\n      pm25\n      humidity\n    }\n    ... on MotionReading {\n      motionDetected\n    }\n  }\n}':
    types.OverviewLatestReadingsDocument,
  'query OverviewSampleCount($from: DateTime!, $to: DateTime!) {\n  readingCounts(filter: {from: $from, to: $to}, groupBy: []) {\n    count\n  }\n}':
    types.OverviewSampleCountDocument,
};

/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 *
 *
 * @example
 * ```ts
 * const query = graphql(`query GetUser($id: ID!) { user(id: $id) { name } }`);
 * ```
 *
 * The query argument is unknown!
 * Please regenerate the types.
 */
export function graphql(source: string): unknown;

/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(
  source: 'query AnalyticsAirQuality($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  airQualityStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n    co2 {\n      average\n      minimum\n      maximum\n    }\n    pm25 {\n      average\n      minimum\n      maximum\n    }\n    humidity {\n      average\n      minimum\n      maximum\n    }\n  }\n}',
): (typeof documents)['query AnalyticsAirQuality($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  airQualityStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n    co2 {\n      average\n      minimum\n      maximum\n    }\n    pm25 {\n      average\n      minimum\n      maximum\n    }\n    humidity {\n      average\n      minimum\n      maximum\n    }\n  }\n}'];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(
  source: 'query AnalyticsAirQualitySummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  airQualityStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    co2 {\n      average\n    }\n    pm25 {\n      average\n    }\n    humidity {\n      average\n    }\n  }\n}',
): (typeof documents)['query AnalyticsAirQualitySummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  airQualityStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    co2 {\n      average\n    }\n    pm25 {\n      average\n    }\n    humidity {\n      average\n    }\n  }\n}'];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(
  source: 'query AnalyticsEnergy($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    samples\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}',
): (typeof documents)['query AnalyticsEnergy($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    samples\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}'];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(
  source: 'query AnalyticsEnergySummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  energyStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}',
): (typeof documents)['query AnalyticsEnergySummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  energyStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}'];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(
  source: 'query AnalyticsMotionActivity($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  motionStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    samples\n    motionSamples\n    activityRatio\n  }\n}',
): (typeof documents)['query AnalyticsMotionActivity($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!, $bucket: TimeBucket!) {\n  motionStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    samples\n    motionSamples\n    activityRatio\n  }\n}'];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(
  source: 'query AnalyticsMotionSummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  motionStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    samples\n    activityRatio\n  }\n}',
): (typeof documents)['query AnalyticsMotionSummary($from: DateTime!, $to: DateTime!, $locations: [MeterLocation!]!) {\n  motionStatistics(\n    filter: {from: $from, to: $to, locations: $locations}\n    groupBy: [LOCATION]\n  ) {\n    location\n    samples\n    activityRatio\n  }\n}'];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(
  source: 'query OverviewEnergySparklines($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    energy {\n      average\n    }\n  }\n}',
): (typeof documents)['query OverviewEnergySparklines($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [LOCATION, TIME]\n    bucket: $bucket\n  ) {\n    location\n    bucketStart\n    energy {\n      average\n    }\n  }\n}'];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(
  source: 'query OverviewEnergyTrend($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}',
): (typeof documents)['query OverviewEnergyTrend($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  energyStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n    energy {\n      average\n      minimum\n      maximum\n    }\n  }\n}'];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(
  source: 'query OverviewIngestionCoverage($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  motionStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n  }\n}',
): (typeof documents)['query OverviewIngestionCoverage($from: DateTime!, $to: DateTime!, $bucket: TimeBucket!) {\n  motionStatistics(\n    filter: {from: $from, to: $to}\n    groupBy: [TIME]\n    bucket: $bucket\n  ) {\n    bucketStart\n    samples\n  }\n}'];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(
  source: 'query OverviewLatestReadings {\n  latestReadings {\n    location\n    receivedAt\n    ... on EnergyReading {\n      energy\n    }\n    ... on AirQualityReading {\n      co2\n      pm25\n      humidity\n    }\n    ... on MotionReading {\n      motionDetected\n    }\n  }\n}',
): (typeof documents)['query OverviewLatestReadings {\n  latestReadings {\n    location\n    receivedAt\n    ... on EnergyReading {\n      energy\n    }\n    ... on AirQualityReading {\n      co2\n      pm25\n      humidity\n    }\n    ... on MotionReading {\n      motionDetected\n    }\n  }\n}'];
/**
 * The graphql function is used to parse GraphQL queries into a document that can be used by GraphQL clients.
 */
export function graphql(
  source: 'query OverviewSampleCount($from: DateTime!, $to: DateTime!) {\n  readingCounts(filter: {from: $from, to: $to}, groupBy: []) {\n    count\n  }\n}',
): (typeof documents)['query OverviewSampleCount($from: DateTime!, $to: DateTime!) {\n  readingCounts(filter: {from: $from, to: $to}, groupBy: []) {\n    count\n  }\n}'];

export function graphql(source: string) {
  return (documents as any)[source] ?? {};
}

export type DocumentType<TDocumentNode extends DocumentNode<any, any>> =
  TDocumentNode extends DocumentNode<infer TType, any> ? TType : never;
