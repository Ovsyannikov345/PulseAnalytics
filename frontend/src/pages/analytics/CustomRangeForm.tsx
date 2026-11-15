import { useState, type SubmitEvent } from 'react';

import { Button } from '../../components/Button';
import { Field } from '../../components/Field';
import { Input } from '../../components/Input';
import { formatDays, toDateTimeLocalValue } from '../../domain/formatting';
import {
  checkRange,
  MAX_STATISTICS_RANGE_MS,
  rangeDurationMs,
  type TimeRange,
} from '../../domain/timeRanges';
import styles from './CustomRangeForm.module.css';

interface Problem {
  field: 'from' | 'to';
  message: string;
}

// `datetime-local` values have no offset, so `Date` reads them as local time.
function findProblem(from: string, to: string): Problem | null {
  if (!from || !to) {
    return { field: from ? 'to' : 'from', message: 'Enter a start and an end.' };
  }

  const range = { from: new Date(from), to: new Date(to) };
  switch (checkRange(range, MAX_STATISTICS_RANGE_MS)) {
    case 'notIncreasing':
      return { field: 'to', message: 'The end must be later than the start.' };
    case 'tooLong':
      return {
        field: 'to',
        message: `Statistics cover up to 31 days. This range is ${formatDays(rangeDurationMs(range))}.`,
      };
    default:
      return null;
  }
}

interface CustomRangeFormProps {
  initialRange: TimeRange;
  onApply: (range: TimeRange) => void;
}

/** From and to (exclusive) to the minute; checked before anything is sent, so the gateway's limits hold. */
export function CustomRangeForm({ initialRange, onApply }: Readonly<CustomRangeFormProps>) {
  const [from, setFrom] = useState(() => toDateTimeLocalValue(initialRange.from));
  const [to, setTo] = useState(() => toDateTimeLocalValue(initialRange.to));
  const [problem, setProblem] = useState<Problem | null>(null);

  function apply(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault();
    const found = findProblem(from, to);
    setProblem(found);
    if (!found) {
      onApply({ from: new Date(from), to: new Date(to) });
    }
  }

  return (
    <form aria-label="Custom range" noValidate className={styles.form} onSubmit={apply}>
      <Field label="From" error={problem?.field === 'from' ? problem.message : null}>
        {(control) => (
          <Input
            {...control}
            type="datetime-local"
            value={from}
            onChange={(event) => {
              setFrom(event.target.value);
            }}
          />
        )}
      </Field>
      <Field label="To (exclusive)" error={problem?.field === 'to' ? problem.message : null}>
        {(control) => (
          <Input
            {...control}
            type="datetime-local"
            value={to}
            onChange={(event) => {
              setTo(event.target.value);
            }}
          />
        )}
      </Field>
      <Button type="submit" variant="primary" className={styles.apply}>
        Apply
      </Button>
    </form>
  );
}
