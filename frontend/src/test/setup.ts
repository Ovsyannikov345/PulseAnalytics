import '@testing-library/jest-dom/vitest';

import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

const OBSERVED_WIDTH = 800;

// jsdom has no ResizeObserver and lays nothing out, so Recharts would size charts to 0: report a fixed
// width. Assigned rather than stubbed, because `unstubGlobals` would remove a stub before each test.
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
