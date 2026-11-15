/** Space kept around the line so the 2 px stroke and its round ends aren't clipped. */
const PADDING = 2;

function round(value: number): string {
  return value.toFixed(1);
}

/**
 * The SVG path of a sparkline: values from 0 (bottom) to `max` (top), evenly spaced across the width.
 * A `null` breaks the line, so gaps stay gaps; an isolated value is drawn as a dot. Values outside
 * 0…`max` are clamped.
 */
export function sparklinePath(
  values: readonly (number | null)[],
  max: number,
  width: number,
  height: number,
): string {
  const step = values.length > 1 ? (width - 2 * PADDING) / (values.length - 1) : 0;
  const plotHeight = height - 2 * PADDING;
  const segments: string[] = [];
  let points: string[] = [];

  const flush = () => {
    if (points.length > 0) {
      // A zero-length segment with round ends draws a dot.
      segments.push(`M${points.join('L')}${points.length === 1 ? 'h0' : ''}`);
    }
    points = [];
  };

  values.forEach((value, index) => {
    if (value === null) {
      flush();
      return;
    }

    const share = max > 0 ? Math.min(1, Math.max(0, value / max)) : 0;
    const x = values.length > 1 ? PADDING + index * step : width / 2;
    points.push(`${round(x)},${round(height - PADDING - share * plotHeight)}`);
  });
  flush();

  return segments.join('');
}
