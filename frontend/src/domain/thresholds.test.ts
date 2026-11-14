import { describe, expect, it } from 'vitest';

import { CO2_WARNING_PPM, PM25_WARNING_UG_PER_M3, isCo2High, isPm25High } from './thresholds';

describe('thresholds', () => {
  it('uses 800 ppm for CO₂ and 35 µg/m³ for PM2.5', () => {
    expect(CO2_WARNING_PPM).toBe(800);
    expect(PM25_WARNING_UG_PER_M3).toBe(35);
  });

  it.each([
    [799, false],
    [800, false],
    [800.1, true],
    [801, true],
  ])('treats CO₂ of %d ppm as high: %s', (ppm, expected) => {
    expect(isCo2High(ppm)).toBe(expected);
  });

  it.each([
    [34, false],
    [35, false],
    [35.1, true],
    [36, true],
  ])('treats PM2.5 of %d µg/m³ as high: %s', (value, expected) => {
    expect(isPm25High(value)).toBe(expected);
  });
});
