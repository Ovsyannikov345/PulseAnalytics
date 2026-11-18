import { formatNumber } from '../../domain/formatting';

/** A meter value, or "—" for a meter without a sample. */
export function formatMeterValue(value: number | null, fractionDigits = 0): string {
  return value === null ? '—' : formatNumber(value, fractionDigits);
}
