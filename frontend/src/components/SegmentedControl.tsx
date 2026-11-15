import { cx } from './cx';
import styles from './SegmentedControl.module.css';

export interface SegmentedOption<TValue extends string> {
  value: TValue;
  label: string;
}

interface SegmentedControlProps<TValue extends string> {
  options: readonly SegmentedOption<TValue>[];
  value: TValue;
  onChange: (value: TValue) => void;
  /** The group's name: `labelledBy` points at a visible label, `label` names it directly. */
  labelledBy?: string;
  label?: string;
  className?: string;
}

/** Toggle buttons of which one is pressed; pressing the pressed one again also reports it. */
export function SegmentedControl<TValue extends string>({
  options,
  value,
  onChange,
  labelledBy,
  label,
  className,
}: Readonly<SegmentedControlProps<TValue>>) {
  return (
    <fieldset
      aria-labelledby={labelledBy}
      aria-label={label}
      className={cx(styles.group, className)}
    >
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          aria-pressed={option.value === value}
          className={styles.segment}
          onClick={() => {
            onChange(option.value);
          }}
        >
          {option.label}
        </button>
      ))}
    </fieldset>
  );
}
