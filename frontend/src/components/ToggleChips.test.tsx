import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { ToggleChips } from './ToggleChips';

const options = [
  { value: 'red', label: 'Red', icon: <svg aria-hidden="true" data-testid="red-icon" /> },
  { value: 'green', label: 'Green' },
  { value: 'blue', label: 'Blue' },
] as const;

describe('ToggleChips', () => {
  it('is a named group that presses the selected options', () => {
    render(
      <ToggleChips label="Colours" options={options} values={['red', 'blue']} onChange={vi.fn()} />,
    );

    expect(screen.getByRole('group', { name: 'Colours' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Red' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: 'Green' })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: 'Blue' })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByTestId('red-icon')).toBeInTheDocument();
  });

  it('can be named by a visible label and described by a message', () => {
    render(
      <>
        <span id="colours-label">Colours</span>
        <ToggleChips
          labelledBy="colours-label"
          describedBy="colours-error"
          options={options}
          values={[]}
          onChange={vi.fn()}
        />
        <span id="colours-error">Pick at least one colour.</span>
      </>,
    );

    expect(screen.getByRole('group', { name: 'Colours' })).toHaveAccessibleDescription(
      'Pick at least one colour.',
    );
  });

  it('adds and removes the clicked option, keeping the options order', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<ToggleChips label="Colours" options={options} values={['blue']} onChange={onChange} />);

    await user.click(screen.getByRole('button', { name: 'Red' }));
    await user.click(screen.getByRole('button', { name: 'Blue' }));

    expect(onChange.mock.calls).toEqual([[['red', 'blue']], [[]]]);
  });
});
