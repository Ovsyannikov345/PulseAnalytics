import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { RefetchDimmer } from './RefetchDimmer';

function region() {
  return screen.getByText('Previous data').closest('[aria-busy]');
}

describe('RefetchDimmer', () => {
  it('keeps the previous data visible under a progress bar while refreshing', () => {
    render(
      <RefetchDimmer refreshing>
        <p>Previous data</p>
      </RefetchDimmer>,
    );

    expect(screen.getByText('Previous data')).toBeVisible();
    expect(screen.getByRole('progressbar', { name: 'Updating' })).toBeInTheDocument();
    // The dimming is styled from aria-busy.
    expect(region()).toHaveAttribute('aria-busy', 'true');
  });

  it('shows the data plainly when not refreshing', () => {
    render(
      <RefetchDimmer refreshing={false}>
        <p>Previous data</p>
      </RefetchDimmer>,
    );

    expect(screen.queryByRole('progressbar')).toBeNull();
    expect(region()).toHaveAttribute('aria-busy', 'false');
  });
});
