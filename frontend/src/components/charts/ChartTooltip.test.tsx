import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ChartTooltip } from './ChartTooltip';

describe('ChartTooltip', () => {
  it('lists the values under the title, with a hidden swatch where a colour is given', () => {
    const { container } = render(
      <ChartTooltip
        title="15 Nov 08:45–09:00"
        rows={[
          { label: 'Kitchen', value: '512.35', color: 'red' },
          { label: 'Samples', value: '342' },
        ]}
      />,
    );

    expect(container).toHaveTextContent('15 Nov 08:45–09:00Kitchen512.35Samples342');
    const swatches = container.querySelectorAll('[aria-hidden="true"]');
    expect(swatches).toHaveLength(1);
    expect(swatches[0]).toHaveAttribute('style', 'background-color: red;');
  });

  it('says when the bucket has no samples', () => {
    const { container } = render(<ChartTooltip title="15 Nov 08:45–09:00" rows={null} />);

    expect(container).toHaveTextContent('15 Nov 08:45–09:00No samples');
  });
});
