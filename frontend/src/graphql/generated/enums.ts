export type MeterLocation =
  'BEDROOM' | 'CORRIDOR' | 'GARAGE' | 'KITCHEN' | 'LIVING_ROOM' | 'OFFICE';

export type MeterType = 'AIR_QUALITY' | 'ENERGY' | 'MOTION';

export type ReadingGrouping = 'LOCATION' | 'TYPE';

export type StatisticsGrouping = 'LOCATION' | 'TIME';

export type TimeBucket = 'DAY' | 'FIFTEEN_MINUTES' | 'FIVE_MINUTES' | 'HOUR' | 'MINUTE';
