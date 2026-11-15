import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

/** The width every observed element reports: jsdom lays nothing out, so all sizes would be 0. */
const OBSERVED_WIDTH = 800;

// jsdom has no ResizeObserver, which Recharts' ResponsiveContainer needs to size charts. This one
// reports a fixed size once, so charts render at 800 px wide. Assigned rather than stubbed with
// `vi.stubGlobal`, because `unstubGlobals` would remove it before each test.
globalThis.ResizeObserver = class {
  private readonly callback: ResizeObserverCallback;

  constructor(callback: ResizeObserverCallback) {
    this.callback = callback;
  }

  observe(target: Element) {
    const contentRect = { width: OBSERVED_WIDTH, height: 0 } as DOMRectReadOnly;
    this.callback([{ target, contentRect } as ResizeObserverEntry], this);
  }

  unobserve() {
    // Sizes never change in jsdom.
  }

  disconnect() {
    // Sizes never change in jsdom.
  }
};

afterEach(() => {
  cleanup();
});
