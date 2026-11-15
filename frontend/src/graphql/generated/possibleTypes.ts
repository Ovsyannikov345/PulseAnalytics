export interface PossibleTypesResultData {
  possibleTypes: {
    [key: string]: string[];
  };
}
const result: PossibleTypesResultData = {
  possibleTypes: {
    MeterReading: ['AirQualityReading', 'EnergyReading', 'MotionReading'],
  },
};
export default result;
