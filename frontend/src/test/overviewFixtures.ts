import type { MeterLocation } from '../graphql/generated/enums';
import type { LatestReading } from '../pages/overview/latestValues';

interface LocationSample {
  energy: number;
  co2: number;
  pm25: number;
  humidity: number;
  motionDetected: boolean;
}

/** The Overview mockup's values: energy sums to 3,012.52, CO₂ averages 672 ppm. */
export const mockupSamples: Record<MeterLocation, LocationSample> = {
  KITCHEN: { energy: 845.62, co2: 803, pm25: 25, humidity: 36, motionDetected: false },
  LIVING_ROOM: { energy: 412.08, co2: 655, pm25: 12, humidity: 48, motionDetected: true },
  BEDROOM: { energy: 128.4, co2: 512, pm25: 8, humidity: 55, motionDetected: false },
  GARAGE: { energy: 946, co2: 921, pm25: 41, humidity: 63, motionDetected: true },
  OFFICE: { energy: 603.17, co2: 744, pm25: 19, humidity: 41, motionDetected: false },
  CORRIDOR: { energy: 77.25, co2: 398, pm25: 6, humidity: 29, motionDetected: false },
};

/** The newest reading of all 18 meters, as `latestReadings` returns them (by type, then location). */
export function latestReadings(
  receivedAt: string,
  samples: Partial<Record<MeterLocation, Partial<LocationSample>>> = {},
): LatestReading[] {
  const locations = Object.keys(mockupSamples) as MeterLocation[];
  const sample = (location: MeterLocation): LocationSample => ({
    ...mockupSamples[location],
    ...samples[location],
  });

  return [
    ...locations.map((location): LatestReading => {
      const { co2, pm25, humidity } = sample(location);
      return { __typename: 'AirQualityReading', location, receivedAt, co2, pm25, humidity };
    }),
    ...locations.map((location): LatestReading => ({
      __typename: 'EnergyReading',
      location,
      receivedAt,
      energy: sample(location).energy,
    })),
    ...locations.map((location): LatestReading => ({
      __typename: 'MotionReading',
      location,
      receivedAt,
      motionDetected: sample(location).motionDetected,
    })),
  ];
}
