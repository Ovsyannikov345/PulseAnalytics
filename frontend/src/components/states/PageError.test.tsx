import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { PageError } from './PageError';

describe('PageError', () => {
  it('alerts that the gateway is unreachable', () => {
    render(<PageError onRetry={vi.fn()} />);

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent("Couldn't load the dashboard");
    expect(alert).toHaveTextContent("The API gateway didn't answer. Your filters are kept.");
    expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent(
      "Couldn't load the dashboard",
    );
  });

  it('retries when Retry is pressed', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(<PageError onRetry={onRetry} />);

    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('can show other texts', () => {
    render(
      <PageError
        onRetry={vi.fn()}
        title="Couldn't load the readings"
        description="Try again in a moment."
      />,
    );

    expect(screen.getByRole('heading', { name: "Couldn't load the readings" })).toBeInTheDocument();
    expect(screen.getByText('Try again in a moment.')).toBeInTheDocument();
  });
});
