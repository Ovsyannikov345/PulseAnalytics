import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ValueHighlight } from './ValueHighlight';

function renderHighlight(value: string | null) {
  const view = render(<ValueHighlight value={value}>{value ?? 'Waiting'}</ValueHighlight>);

  return {
    rerender: (next: string | null) => {
      view.rerender(<ValueHighlight value={next}>{next ?? 'Waiting'}</ValueHighlight>);
    },
    highlighted: () => screen.getByText(/./).hasAttribute('data-highlighted'),
    element: () => screen.getByText(/./),
  };
}

describe('ValueHighlight', () => {
  it('does not highlight the first value', () => {
    const view = renderHighlight('845.62');

    expect(view.highlighted()).toBe(false);
  });

  it('does not highlight the first value to arrive after waiting', () => {
    const view = renderHighlight(null);

    view.rerender('845.62');

    expect(view.element()).toHaveTextContent('845.62');
    expect(view.highlighted()).toBe(false);
  });

  it('highlights a changed value', () => {
    const view = renderHighlight('845.62');

    view.rerender('912.85');

    expect(view.element()).toHaveTextContent('912.85');
    expect(view.highlighted()).toBe(true);
  });

  it('keeps the element for an equal value and replaces it for each change', () => {
    const view = renderHighlight('845.62');
    view.rerender('912.85');
    const afterFirstChange = view.element();

    view.rerender('912.85');
    expect(view.element()).toBe(afterFirstChange);

    view.rerender('845.62');
    expect(view.element()).not.toBe(afterFirstChange);
    expect(view.highlighted()).toBe(true);
  });

  it('highlights a chip with a ring', () => {
    const { rerender } = render(
      <ValueHighlight value={false} variant="ring">
        No motion
      </ValueHighlight>,
    );

    rerender(
      <ValueHighlight value variant="ring">
        Motion
      </ValueHighlight>,
    );

    expect(screen.getByText('Motion')).toHaveAttribute('data-highlighted', 'true');
  });
});
