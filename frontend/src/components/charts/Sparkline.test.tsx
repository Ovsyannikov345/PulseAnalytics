import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Sparkline } from './Sparkline';

describe('Sparkline', () => {
  it('is an image named by its label', () => {
    render(<Sparkline values={[0, 1.5, 3]} max={3} label="Energy trend over the last 2 hours" />);

    const image = screen.getByRole('img', { name: 'Energy trend over the last 2 hours' });
    expect(image).toHaveAttribute('viewBox', '0 0 132 36');
    expect(image.querySelector('path')).toHaveAttribute('d', 'M2.0,34.0L66.0,18.0L130.0,2.0');
  });

  it('draws no line when every value is missing', () => {
    render(<Sparkline values={[null, null]} max={3} label="Energy trend" />);

    expect(screen.getByRole('img', { name: 'Energy trend' }).querySelector('path')).toBeNull();
  });
});
