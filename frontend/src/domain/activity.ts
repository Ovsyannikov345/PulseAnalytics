/** Motion activity is shown in five classes of 20 % each: 0–20 %, 20–40 %, … 80–100 %. */
export const ACTIVITY_CLASS_COUNT = 5;

/** The class of a share of samples with motion, from 0 (0–20 %) to 4 (80–100 %); a boundary starts the higher class. */
export function activityClass(ratio: number): number {
  const index = Math.floor(ratio * ACTIVITY_CLASS_COUNT);
  return Math.min(ACTIVITY_CLASS_COUNT - 1, Math.max(0, index));
}

/** The class's range as a label: "20–40 %". */
export function activityClassLabel(index: number): string {
  const step = 100 / ACTIVITY_CLASS_COUNT;
  return `${String(index * step)}–${String((index + 1) * step)} %`;
}
