/** CO₂ concentration, ppm. */
export const CO2_WARNING_PPM = 800;

/** PM2.5 concentration, µg/m³. */
export const PM25_WARNING_UG_PER_M3 = 35;

export function isCo2High(ppm: number): boolean {
  return ppm > CO2_WARNING_PPM;
}

export function isPm25High(microgramsPerCubicMetre: number): boolean {
  return microgramsPerCubicMetre > PM25_WARNING_UG_PER_M3;
}
