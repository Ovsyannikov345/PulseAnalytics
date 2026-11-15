import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { WidgetError } from './WidgetError';

describe('WidgetError', () => {
  it('announces the message politely', () => {
    render(<WidgetError message="This chart couldn't be loaded." onRetry={vi.fn()} />);

    expect(screen.getByRole('status')).toHaveTextContent("This chart couldn't be loaded.");
  });

  it('retries when Retry is pressed', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(<WidgetError message="This chart couldn't be loaded." onRetry={onRetry} />);

    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(onRetry).toHaveBeenCalledOnce();
  });

  it("describes each Retry button with its own widget's message", () => {
    render(
      <>
        <WidgetError message="This chart couldn't be loaded." onRetry={vi.fn()} />
        <WidgetError message="The readings couldn't be loaded." onRetry={vi.fn()} />
      </>,
    );

    const [chartRetry, tableRetry] = screen.getAllByRole('button', { name: 'Retry' });
    expect(chartRetry).toHaveAccessibleDescription("This chart couldn't be loaded.");
    expect(tableRetry).toHaveAccessibleDescription("The readings couldn't be loaded.");
  });
});
