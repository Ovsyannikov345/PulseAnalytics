import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { SegmentedControl } from './SegmentedControl';

const options = [
  { value: 'combined', label: 'Combined' },
  { value: 'byLocation', label: 'By location' },
] as const;

describe('SegmentedControl', () => {
  it('is a named group that presses the selected option', () => {
    render(
      <SegmentedControl
        label="Energy view"
        options={options}
        value="byLocation"
        onChange={vi.fn()}
      />,
    );

    expect(screen.getByRole('group', { name: 'Energy view' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'By location' })).toHaveAttribute(
      'aria-pressed',
      'true',
    );
    expect(screen.getByRole('button', { name: 'Combined' })).toHaveAttribute(
      'aria-pressed',
      'false',
    );
  });

  it('can be named by a visible label', () => {
    render(
      <>
        <span id="view-label">View</span>
        <SegmentedControl
          labelledBy="view-label"
          options={options}
          value="combined"
          onChange={vi.fn()}
        />
      </>,
    );

    expect(screen.getByRole('group', { name: 'View' })).toBeInTheDocument();
  });

  it('reports the clicked option, also when it is already pressed', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <SegmentedControl label="View" options={options} value="combined" onChange={onChange} />,
    );

    await user.click(screen.getByRole('button', { name: 'By location' }));
    await user.click(screen.getByRole('button', { name: 'Combined' }));

    expect(onChange.mock.calls).toEqual([['byLocation'], ['combined']]);
  });
});
