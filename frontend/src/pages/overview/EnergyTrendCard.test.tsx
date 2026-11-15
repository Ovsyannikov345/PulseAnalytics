import { render } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { EnergyTooltip } from './EnergyTrendCard';

describe('EnergyTooltip', () => {
  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  it("shows the bucket's time span, average, range and samples", () => {
    const { container } = render(
      <EnergyTooltip
        point={{
          time: Date.parse('2026-11-15T05:45:00Z'),
          value: 512.345,
          bucket: { average: 512.345, minimum: 12.5, maximum: 989.25, samples: 342 },
        }}
      />,
    );

    expect(container).toHaveTextContent(
      '15 Nov 08:45–09:00Average512.35Min – max12.50 – 989.25Samples342',
    );
  });

  it('says when the bucket has no samples', () => {
    const { container } = render(
      <EnergyTooltip
        point={{ time: Date.parse('2026-11-15T05:45:00Z'), value: null, bucket: null }}
      />,
    );

    expect(container).toHaveTextContent('15 Nov 08:45–09:00No samples');
  });
});
