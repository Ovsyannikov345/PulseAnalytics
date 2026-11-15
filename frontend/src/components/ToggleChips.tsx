import type { ReactNode } from 'react';

import { cx } from './cx';
import styles from './ToggleChips.module.css';

export interface ToggleChipOption<TValue extends string> {
  value: TValue;
  label: string;
  /** Decorative; hidden from screen readers by the icon itself. */
  icon?: ReactNode;
}

interface ToggleChipsProps<TValue extends string> {
  options: readonly ToggleChipOption<TValue>[];
  values: readonly TValue[];
  /** The new selection, in the options' order; it can be empty. */
  onChange: (values: TValue[]) => void;
  /** The group's name: `labelledBy` points at a visible label, `label` names it directly. */
  labelledBy?: string;
  label?: string;
  /** Ids of texts that describe the group, such as an error message. */
  describedBy?: string;
  className?: string;
}

/** Toggle buttons of which any number are pressed: the multi-select counterpart of `SegmentedControl`. */
export function ToggleChips<TValue extends string>({
  options,
  values,
  onChange,
  labelledBy,
  label,
  describedBy,
  className,
}: Readonly<ToggleChipsProps<TValue>>) {
  function toggle(toggled: TValue) {
    onChange(
      options
        .filter(({ value }) =>
          value === toggled ? !values.includes(value) : values.includes(value),
        )
        .map(({ value }) => value),
    );
  }

  return (
    <fieldset
      aria-labelledby={labelledBy}
      aria-label={label}
      aria-describedby={describedBy}
      className={cx(styles.group, className)}
    >
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          aria-pressed={values.includes(option.value)}
          className={styles.chip}
          onClick={() => {
            toggle(option.value);
          }}
        >
          {option.icon}
          {option.label}
        </button>
      ))}
    </fieldset>
  );
}
