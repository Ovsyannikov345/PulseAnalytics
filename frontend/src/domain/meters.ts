import type { MeterLocation, MeterType } from '../graphql/generated/enums';

interface EnumNames {
  /** Shown in the UI. */
  label: string;
  /** The C# enum name the REST export binds (`AirQuality`), unlike GraphQL's `AIR_QUALITY`. */
  restName: string;
}

// Records over the generated unions: a value added to the schema fails the type check until it is named here.
// Listed in the gateway's order (its enum values), which is also the order of its results.
const meterTypeNames: Record<MeterType, EnumNames> = {
  AIR_QUALITY: { label: 'Air quality', restName: 'AirQuality' },
  ENERGY: { label: 'Energy', restName: 'Energy' },
  MOTION: { label: 'Motion', restName: 'Motion' },
};

const meterLocationNames: Record<MeterLocation, EnumNames> = {
  KITCHEN: { label: 'Kitchen', restName: 'Kitchen' },
  LIVING_ROOM: { label: 'Living Room', restName: 'LivingRoom' },
  BEDROOM: { label: 'Bedroom', restName: 'Bedroom' },
  GARAGE: { label: 'Garage', restName: 'Garage' },
  OFFICE: { label: 'Office', restName: 'Office' },
  CORRIDOR: { label: 'Corridor', restName: 'Corridor' },
};

export const meterTypes = Object.keys(meterTypeNames) as readonly MeterType[];

export const meterLocations = Object.keys(meterLocationNames) as readonly MeterLocation[];

/** WeakApp has one meter of each type in each location. */
export const METERS_PER_TYPE = meterLocations.length;

export const METER_COUNT = meterTypes.length * METERS_PER_TYPE;

export function meterTypeLabel(type: MeterType): string {
  return meterTypeNames[type].label;
}

export function meterTypeRestName(type: MeterType): string {
  return meterTypeNames[type].restName;
}

export function meterLocationLabel(location: MeterLocation): string {
  return meterLocationNames[location].label;
}

export function meterLocationRestName(location: MeterLocation): string {
  return meterLocationNames[location].restName;
}
