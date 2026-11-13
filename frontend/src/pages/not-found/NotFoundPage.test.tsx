import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { renderRoute } from '../../test/renderRoute';

describe('NotFoundPage', () => {
  it('is shown for an unknown address, with no page marked in the nav', async () => {
    renderRoute('/no-such-page');

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Page not found' }),
    ).toBeInTheDocument();
    const nav = screen.getByRole('navigation', { name: 'Main' });
    for (const link of within(nav).getAllByRole('link')) {
      expect(link).not.toHaveAttribute('aria-current');
    }
  });

  it('links to the overview', async () => {
    const user = userEvent.setup();
    const router = renderRoute('/no-such-page');

    await user.click(screen.getByRole('link', { name: 'Go to the overview' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Overview' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
  });
});
