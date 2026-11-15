import { useId, useState, type SubmitEvent } from 'react';

import { Button } from '../../components/Button';
import { Field, FieldError } from '../../components/Field';
import { Input } from '../../components/Input';
import { Select } from '../../components/Select';
import { ToggleChips, type ToggleChipOption } from '../../components/ToggleChips';
import { formatNumber } from '../../domain/formatting';
import {
  meterLocationLabel,
  meterLocations,
  meterTypeLabel,
  meterTypes,
} from '../../domain/meters';
import type { MeterType } from '../../graphql/generated/enums';
import { MeterTypeIcon } from './MeterTypeIcon';
import {
  readDraft,
  toDraft,
  type DraftProblems,
  type ReadingsDraft,
  type ReadingsFilter,
} from './readingsFilter';
import styles from './ReadingsFilterForm.module.css';

const typeOptions: readonly ToggleChipOption<MeterType>[] = meterTypes.map((type) => ({
  value: type,
  label: meterTypeLabel(type),
  icon: <MeterTypeIcon type={type} />,
}));

interface ReadingsFilterFormProps {
  /** The applied filter; the form starts from it. Remount the form (`key`) to refill it. */
  filter: ReadingsFilter;
  onApply: (filter: ReadingsFilter) => void;
  onReset: () => void;
}

/** Meter types, location and time range, applied together; checked before anything is sent. */
export function ReadingsFilterForm({
  filter,
  onApply,
  onReset,
}: Readonly<ReadingsFilterFormProps>) {
  const typesLabelId = useId();
  const typesErrorId = useId();
  const [draft, setDraft] = useState<ReadingsDraft>(() => toDraft(filter));
  const [problems, setProblems] = useState<DraftProblems>({});

  function change(values: Partial<ReadingsDraft>) {
    setDraft((current) => ({ ...current, ...values }));
  }

  function apply(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const result = readDraft(draft);
    setProblems(result.problems ?? {});
    if (result.filter) {
      onApply(result.filter);
    }
  }

  return (
    <section aria-label="Filters" className={styles.card}>
      <form aria-label="Filter readings" noValidate className={styles.form} onSubmit={apply}>
        <div className={styles.types}>
          <span id={typesLabelId} className={styles.groupLabel}>
            Meter types
          </span>
          <ToggleChips
            labelledBy={typesLabelId}
            describedBy={problems.types ? typesErrorId : undefined}
            options={typeOptions}
            values={draft.types}
            onChange={(types) => {
              change({ types });
            }}
          />
          {problems.types ? <FieldError id={typesErrorId} message={problems.types} /> : null}
        </div>
        <Field label="Locations">
          {(control) => (
            <Select
              {...control}
              value={draft.location ?? ''}
              onChange={(event) => {
                change({
                  location: meterLocations.find((value) => value === event.target.value) ?? null,
                });
              }}
            >
              <option value="">All locations ({formatNumber(meterLocations.length)})</option>
              {meterLocations.map((location) => (
                <option key={location} value={location}>
                  {meterLocationLabel(location)}
                </option>
              ))}
            </Select>
          )}
        </Field>
        <Field label="From" error={problems.from}>
          {(control) => (
            <Input
              {...control}
              type="datetime-local"
              value={draft.from}
              className={styles.time}
              onChange={(event) => {
                change({ from: event.target.value });
              }}
            />
          )}
        </Field>
        <Field label="To (exclusive)" error={problems.to}>
          {(control) => (
            <Input
              {...control}
              type="datetime-local"
              value={draft.to}
              className={styles.time}
              onChange={(event) => {
                change({ to: event.target.value });
              }}
            />
          )}
        </Field>
        <div className={styles.actions}>
          <Button onClick={onReset}>Reset</Button>
          <Button type="submit" variant="primary">
            Apply
          </Button>
        </div>
      </form>
    </section>
  );
}
