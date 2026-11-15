import { describe, expect, it } from 'vitest';

import {
  METER_COUNT,
  METERS_PER_TYPE,
  meterLocationLabel,
  meterLocationRestName,
  meterLocations,
  meterTypeLabel,
  meterTypeRestName,
  meterTypes,
} from './meters';

describe('meters', () => {
  it('lists the types and locations in the gateway order', () => {
    expect(meterTypes).toEqual(['AIR_QUALITY', 'ENERGY', 'MOTION']);
    expect(meterLocations).toEqual([
      'KITCHEN',
      'LIVING_ROOM',
      'BEDROOM',
      'GARAGE',
      'OFFICE',
      'CORRIDOR',
    ]);
  });

  it('counts one meter per type and location', () => {
    expect(METERS_PER_TYPE).toBe(6);
    expect(METER_COUNT).toBe(18);
  });

  it.each([
    ['AIR_QUALITY', 'Air quality', 'AirQuality'],
    ['ENERGY', 'Energy', 'Energy'],
    ['MOTION', 'Motion', 'Motion'],
  ] as const)('names the type %s "%s" and %s for REST', (type, label, restName) => {
    expect(meterTypeLabel(type)).toBe(label);
    expect(meterTypeRestName(type)).toBe(restName);
  });

  it.each([
    ['KITCHEN', 'Kitchen', 'Kitchen'],
    ['LIVING_ROOM', 'Living Room', 'LivingRoom'],
    ['BEDROOM', 'Bedroom', 'Bedroom'],
    ['GARAGE', 'Garage', 'Garage'],
    ['OFFICE', 'Office', 'Office'],
    ['CORRIDOR', 'Corridor', 'Corridor'],
  ] as const)('names the location %s "%s" and %s for REST', (location, label, restName) => {
    expect(meterLocationLabel(location)).toBe(label);
    expect(meterLocationRestName(location)).toBe(restName);
  });
});
