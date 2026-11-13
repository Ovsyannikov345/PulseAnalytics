import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderRoute } from '../test/renderRoute';

function mainNav() {
  return screen.getByRole('navigation', { name: 'Main' });
}

function navLink(name: string) {
  return within(mainNav()).getByRole('link', { name });
}

describe('AppLayout', () => {
  beforeEach(() => {
    // UTC+03:00
    vi.spyOn(Date.prototype, 'getTimezoneOffset').mockReturnValue(-180);
  });

  it.each([
    ['/', 'Overview'],
    ['/analytics', 'Analytics'],
    ['/readings', 'Readings'],
  ])('marks only the current page in the nav on %s', async (path, page) => {
    renderRoute(path);

    expect(await screen.findByRole('heading', { level: 1, name: page })).toBeInTheDocument();
    for (const link of within(mainNav()).getAllByRole('link')) {
      if (link.textContent === page) {
        expect(link).toHaveAttribute('aria-current', 'page');
      } else {
        expect(link).not.toHaveAttribute('aria-current');
      }
    }
  });

  it('opens the page picked in the nav', async () => {
    const user = userEvent.setup();
    const router = renderRoute('/');

    await user.click(navLink('Readings'));

    expect(await screen.findByRole('heading', { level: 1, name: 'Readings' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/readings');
    expect(navLink('Readings')).toHaveAttribute('aria-current', 'page');
    expect(navLink('Overview')).not.toHaveAttribute('aria-current');
  });

  it('links the brand to the overview', async () => {
    const user = userEvent.setup();
    const router = renderRoute('/analytics');

    await user.click(screen.getByRole('link', { name: 'PulseAnalytics' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Overview' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/');
  });

  it('shows the local time zone in the header', () => {
    renderRoute('/');

    expect(screen.getByRole('banner')).toHaveTextContent('Local time (UTC+03:00)');
  });

  it('sets the document title of the page', async () => {
    renderRoute('/analytics');

    await waitFor(() => {
      expect(document.title).toBe('Analytics · PulseAnalytics');
    });
  });

  it('has a skip link to the main content', () => {
    renderRoute('/');

    expect(screen.getByRole('link', { name: 'Skip to content' })).toHaveAttribute(
      'href',
      '#content',
    );
    expect(screen.getByRole('main')).toHaveAttribute('id', 'content');
  });
});
