import { meterLocations } from '../../domain/meters';
import type { MeterLocation } from '../../graphql/generated/enums';

/** Each location's series colour, from the Analytics board (`--color-series-1…6`, in gateway order). */
export function locationColor(location: MeterLocation): string {
  return `var(--color-series-${String(meterLocations.indexOf(location) + 1)})`;
}
