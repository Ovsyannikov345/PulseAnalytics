const PADDING = 2;

function round(value: number): string {
  return value.toFixed(1);
}

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
