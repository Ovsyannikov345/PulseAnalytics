import { formatNumber } from '../../domain/formatting';
import {
  CO2_WARNING_PPM,
  isCo2High,
  isPm25High,
  PM25_WARNING_UG_PER_M3,
} from '../../domain/thresholds';
import { Chip } from '../Chip';
import styles from './AirQualityWarnings.module.css';

interface AirQualityWarningsProps {
  airQuality: { co2: number; pm25: number } | null;
}

/** Warning chips for CO₂ and PM2.5 above their thresholds; nothing when both are fine. */
export function AirQualityWarnings({ airQuality }: Readonly<AirQualityWarningsProps>) {
  const warnings = [
    airQuality && isCo2High(airQuality.co2)
      ? `CO₂ above ${formatNumber(CO2_WARNING_PPM)} ppm`
      : null,
    airQuality && isPm25High(airQuality.pm25)
      ? `PM2.5 above ${formatNumber(PM25_WARNING_UG_PER_M3)} µg/m³`
      : null,
  ].filter((warning) => warning !== null);

  if (warnings.length === 0) {
    return null;
  }

  return (
    <div className={styles.warnings}>
      {warnings.map((warning) => (
        <Chip key={warning} variant="warning">
          {warning}
        </Chip>
      ))}
    </div>
  );
}
