import { describe, expect, it } from 'vitest';

// The notification service's snapshot tests write these with the hub's JSON protocol options.
import airQualityExample from '../../../services/NotificationService/tests/NotificationService.UnitTests/Api/Hubs/Readings/Snapshots/air-quality.json?raw';
import energyExample from '../../../services/NotificationService/tests/NotificationService.UnitTests/Api/Hubs/Readings/Snapshots/energy.json?raw';
import motionExample from '../../../services/NotificationService/tests/NotificationService.UnitTests/Api/Hubs/Readings/Snapshots/motion.json?raw';
import { parseLiveReading } from './liveReadingMessage';

const airQuality = JSON.parse(airQualityExample) as Record<string, unknown>;
const energy = JSON.parse(energyExample) as Record<string, unknown>;
const motion = JSON.parse(motionExample) as Record<string, unknown>;

describe('parseLiveReading', () => {
  it('reads the hub examples', () => {
    expect(parseLiveReading(motion)).toEqual({
      type: 'MOTION',
      id: '019a91b2-4c6e-7d13-9a58-3e7f2b6c1d01',
      location: 'GARAGE',
      receivedAt: '2026-11-17T10:15:30.123+00:00',
      motionDetected: false,
    });
    expect(parseLiveReading(energy)).toEqual({
      type: 'ENERGY',
      id: '019a91b2-4c6e-7d13-9a58-3e7f2b6c1d02',
      location: 'OFFICE',
      receivedAt: '2026-11-17T10:15:30.123+00:00',
      energy: 845.62,
    });
    expect(parseLiveReading(airQuality)).toEqual({
      type: 'AIR_QUALITY',
      id: '019a91b2-4c6e-7d13-9a58-3e7f2b6c1d03',
      location: 'LIVING_ROOM',
      receivedAt: '2026-11-17T10:15:30.123+00:00',
      co2: 803,
      pm25: 25,
      humidity: 36,
    });
  });

  it('ignores fields it does not know', () => {
    expect(parseLiveReading({ ...energy, unit: 'kWh' })).toEqual(parseLiveReading(energy));
  });

  it('reads times with more than three fraction digits', () => {
    expect(
      parseLiveReading({ ...energy, receivedAt: '2026-11-17T10:15:30.1234567+00:00' }),
    ).toMatchObject({ receivedAt: '2026-11-17T10:15:30.1234567+00:00' });
  });

  it.each([null, undefined, 'Energy', 42, [energy]])('drops a message that is %j', (message) => {
    expect(parseLiveReading(message)).toBeNull();
  });

  it.each([
    ['an unknown type', { ...energy, type: 'Water' }],
    ["GraphQL's type name", { ...energy, type: 'ENERGY' }],
    ['an unknown location', { ...energy, location: 'Attic' }],
    ["GraphQL's location name", { ...airQuality, location: 'LIVING_ROOM' }],
    ['a location number', { ...energy, location: 99 }],
    ['a missing type', { ...energy, type: undefined }],
    ['a missing id', { ...energy, id: undefined }],
    ['an empty id', { ...energy, id: '' }],
    ['a missing time', { ...energy, receivedAt: undefined }],
    ['a time that does not parse', { ...energy, receivedAt: 'yesterday' }],
    ['a missing energy', { ...energy, energy: undefined }],
    ['an energy string', { ...energy, energy: '845.62' }],
    ['a motion number', { ...motion, motionDetected: 0 }],
    ['a missing co2', { ...airQuality, co2: undefined }],
    ['a missing pm25', { ...airQuality, pm25: null }],
    ['a humidity string', { ...airQuality, humidity: '36' }],
    ['energy values on an air quality type', { ...energy, type: 'AirQuality' }],
  ])('drops a message with %s', (_, message) => {
    expect(parseLiveReading(message)).toBeNull();
  });
});
