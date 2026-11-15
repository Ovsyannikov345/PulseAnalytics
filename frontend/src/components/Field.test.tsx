import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Field } from './Field';
import { Input } from './Input';
import { Select } from './Select';

describe('Field', () => {
  it('names its control by the label and describes nothing by default', () => {
    render(<Field label="From">{(control) => <Input {...control} />}</Field>);

    const input = screen.getByRole('textbox', { name: 'From' });
    expect(input).not.toHaveAttribute('aria-describedby');
    expect(input).not.toHaveAttribute('aria-invalid');
  });

  it('describes the control by its hint', () => {
    render(
      <Field label="Bucket" hint="Days start at 03:00 local time (00:00 UTC)">
        {(control) => (
          <Select {...control}>
            <option>1 day</option>
          </Select>
        )}
      </Field>,
    );

    expect(screen.getByRole('combobox', { name: 'Bucket' })).toHaveAccessibleDescription(
      'Days start at 03:00 local time (00:00 UTC)',
    );
  });

  it('marks the control invalid and describes it by the error first', () => {
    render(
      <Field label="To" hint="Exclusive" error="The end must be later than the start.">
        {(control) => <Input {...control} />}
      </Field>,
    );

    const input = screen.getByRole('textbox', { name: 'To' });
    expect(input).toHaveAttribute('aria-invalid', 'true');
    expect(input).toHaveAccessibleDescription('The end must be later than the start. Exclusive');
  });
});
