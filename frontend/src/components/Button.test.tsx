import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { Button } from './Button';

describe('Button', () => {
  it('is a plain button by default, so it never submits a form by accident', () => {
    render(<Button>Retry</Button>);

    expect(screen.getByRole('button', { name: 'Retry' })).toHaveAttribute('type', 'button');
  });

  it('can be a submit button', () => {
    render(<Button type="submit">Apply</Button>);

    expect(screen.getByRole('button', { name: 'Apply' })).toHaveAttribute('type', 'submit');
  });

  it('calls onClick when pressed', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(
      <Button variant="primary" size="compact" onClick={onClick}>
        Retry
      </Button>,
    );

    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(onClick).toHaveBeenCalledOnce();
  });

  it('ignores presses while disabled', async () => {
    const user = userEvent.setup();
    const onClick = vi.fn();
    render(
      <Button disabled onClick={onClick}>
        Export CSV
      </Button>,
    );

    await user.click(screen.getByRole('button', { name: 'Export CSV' }));

    expect(screen.getByRole('button', { name: 'Export CSV' })).toBeDisabled();
    expect(onClick).not.toHaveBeenCalled();
  });

  it('keeps a class name passed by the caller', () => {
    render(<Button className="extra">Retry</Button>);

    expect(screen.getByRole('button', { name: 'Retry' })).toHaveClass('extra');
  });
});
