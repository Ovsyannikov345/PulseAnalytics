import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { renderRoute } from '../../test/renderRoute';

// 14:29:47 in Moscow: the default range is 13:30–14:30.
const opened = new Date('2026-11-15T11:29:47Z');

const defaultSearch = '?from=2026-11-15T10%3A30%3A00.000Z&to=2026-11-15T11%3A30%3A00.000Z';

function typeChip(name: string) {
  return within(screen.getByRole('group', { name: 'Meter types' })).getByRole('button', { name });
}

function setTime(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
}

describe('ReadingsPage filters', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(opened);
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('opens with every meter over the last hour', () => {
    renderRoute('/readings');

    expect(screen.getByRole('heading', { level: 1, name: 'Readings' })).toBeInTheDocument();
    for (const name of ['Air quality', 'Energy', 'Motion']) {
      expect(typeChip(name)).toHaveAttribute('aria-pressed', 'true');
    }
    expect(screen.getByRole('combobox', { name: 'Locations' })).toHaveDisplayValue(
      'All locations (6)',
    );
    expect(screen.getByLabelText('From')).toHaveValue('2026-11-15T13:30');
    expect(screen.getByLabelText('To (exclusive)')).toHaveValue('2026-11-15T14:30');
  });

  it('opens a link with every filter', () => {
    renderRoute(
      '/readings?from=2026-11-14T06:00:00Z&to=2026-11-14T07:00:00Z&type=ENERGY&location=KITCHEN',
    );

    expect(typeChip('Air quality')).toHaveAttribute('aria-pressed', 'false');
    expect(typeChip('Energy')).toHaveAttribute('aria-pressed', 'true');
    expect(typeChip('Motion')).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('combobox', { name: 'Locations' })).toHaveDisplayValue('Kitchen');
    expect(screen.getByLabelText('From')).toHaveValue('2026-11-14T09:00');
    expect(screen.getByLabelText('To (exclusive)')).toHaveValue('2026-11-14T10:00');
  });

  it('applies the filters together and keeps them in the URL', async () => {
    const user = userEvent.setup();
    const router = renderRoute('/readings');

    await user.click(typeChip('Air quality'));
    await user.selectOptions(screen.getByRole('combobox', { name: 'Locations' }), 'Garage');
    setTime('From', '2026-11-15T12:00');
    expect(router.state.location.search).toBe('');

    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(router.state.location.search).toBe(
      '?from=2026-11-15T09%3A00%3A00.000Z&to=2026-11-15T11%3A30%3A00.000Z&type=ENERGY&type=MOTION&location=GARAGE',
    );
  });

  it('drops the location from the URL for all locations', async () => {
    const user = userEvent.setup();
    const router = renderRoute(`/readings${defaultSearch}&location=OFFICE`);

    await user.selectOptions(
      screen.getByRole('combobox', { name: 'Locations' }),
      'All locations (6)',
    );
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(router.state.location.search).toBe(defaultSearch);
  });

  it('shows problems at their controls and applies nothing', async () => {
    const user = userEvent.setup();
    const router = renderRoute('/readings');

    for (const name of ['Air quality', 'Energy', 'Motion']) {
      await user.click(typeChip(name));
    }
    setTime('To (exclusive)', '2026-11-15T13:00');
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(screen.getByRole('group', { name: 'Meter types' })).toHaveAccessibleDescription(
      'Pick at least one meter type.',
    );
    expect(screen.getByLabelText('To (exclusive)')).toHaveAccessibleDescription(
      'The end must be later than the start.',
    );
    expect(screen.getByLabelText('From')).not.toHaveAttribute('aria-invalid');
    expect(router.state.location.search).toBe('');

    setTime('From', '');
    await user.click(screen.getByRole('button', { name: 'Apply' }));

    expect(screen.getByLabelText('From')).toHaveAccessibleDescription('Enter a start and an end.');
  });

  it('resets to every meter over the hour up to now', async () => {
    const user = userEvent.setup();
    const router = renderRoute('/readings?type=MOTION&location=OFFICE');

    vi.setSystemTime(opened.getTime() + 10 * 60_000);
    await user.click(screen.getByRole('button', { name: 'Reset' }));

    expect(router.state.location.search).toBe(
      '?from=2026-11-15T10%3A40%3A00.000Z&to=2026-11-15T11%3A40%3A00.000Z',
    );
    expect(typeChip('Air quality')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('combobox', { name: 'Locations' })).toHaveDisplayValue(
      'All locations (6)',
    );
    expect(screen.getByLabelText('To (exclusive)')).toHaveValue('2026-11-15T14:40');
  });

  it('clears the form on Reset when the default filter is already applied', async () => {
    const user = userEvent.setup();
    const router = renderRoute(`/readings${defaultSearch}`);

    await user.click(typeChip('Energy'));
    setTime('From', '');
    await user.click(screen.getByRole('button', { name: 'Apply' }));
    expect(screen.getByLabelText('From')).toHaveAttribute('aria-invalid', 'true');

    await user.click(screen.getByRole('button', { name: 'Reset' }));

    expect(router.state.location.search).toBe(defaultSearch);
    expect(router.state.historyAction).toBe('REPLACE');
    expect(typeChip('Energy')).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByLabelText('From')).toHaveValue('2026-11-15T13:30');
    expect(screen.getByLabelText('From')).not.toHaveAttribute('aria-invalid');
  });

  it('refills the form with the previous filters on the back button', async () => {
    const user = userEvent.setup();
    const router = renderRoute('/readings');

    await user.click(typeChip('Motion'));
    await user.click(screen.getByRole('button', { name: 'Apply' }));
    expect(typeChip('Motion')).toHaveAttribute('aria-pressed', 'false');

    await act(() => router.navigate(-1));

    expect(router.state.location.search).toBe('');
    expect(typeChip('Motion')).toHaveAttribute('aria-pressed', 'true');
  });
});
