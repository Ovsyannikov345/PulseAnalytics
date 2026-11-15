import { fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { INVALID_FILTER_MESSAGE } from '../../graphql/errors';
import {
  defaultAnalyticsFilter,
  effectiveBucket,
  resolveRange,
  type AnalyticsFilter,
} from './analyticsFilter';
import { FilterBar } from './FilterBar';

// 14:30 in Moscow.
const now = new Date('2026-11-15T11:30:00Z');

function renderBar(filter: AnalyticsFilter = defaultAnalyticsFilter, invalidInput = false) {
  const onChange = vi.fn<(filter: AnalyticsFilter) => void>();
  const range = resolveRange(filter.range, now);
  render(
    <FilterBar
      filter={filter}
      range={range}
      bucket={effectiveBucket(filter.bucket, range)}
      invalidInput={invalidInput}
      onChange={onChange}
    />,
  );
  return onChange;
}

function rangeButton(name: string) {
  return within(screen.getByRole('group', { name: 'Time range' })).getByRole('button', { name });
}

function bucketOption(name: string | RegExp) {
  return within(screen.getByRole('combobox', { name: 'Bucket' })).getByRole('option', { name });
}

describe('FilterBar', () => {
  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  it('shows the applied filters', () => {
    renderBar({ ...defaultAnalyticsFilter, location: 'KITCHEN' });

    expect(screen.getByRole('region', { name: 'Filters' })).toBeInTheDocument();
    expect(rangeButton('24 h')).toHaveAttribute('aria-pressed', 'true');
    expect(rangeButton('Custom…')).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('combobox', { name: 'Bucket' })).toHaveDisplayValue(
      'Auto (15 minutes)',
    );
    expect(screen.getByRole('combobox', { name: 'Locations' })).toHaveDisplayValue('Kitchen');
    expect(screen.queryByRole('form', { name: 'Custom range' })).not.toBeInTheDocument();
  });

  it('describes the range and its buckets', () => {
    renderBar();

    expect(
      screen.getByText(
        "14 Nov 14:30 – 15 Nov 14:30 · 96 buckets. Bucket sizes that would need more than 1,000 buckets for this range can't be picked.",
      ),
    ).toBeInTheDocument();
  });

  it('leaves out the bucket limit when every size fits', () => {
    renderBar({ ...defaultAnalyticsFilter, range: { kind: 'preset', preset: '1h' } });

    expect(screen.getByText('15 Nov 13:30 – 15 Nov 14:30 · 60 buckets.')).toBeInTheDocument();
    expect(bucketOption('1 minute')).toBeEnabled();
  });

  it('counts a single bucket', () => {
    renderBar({
      ...defaultAnalyticsFilter,
      range: {
        kind: 'custom',
        range: { from: new Date('2026-11-10T00:00:00Z'), to: new Date('2026-11-10T12:00:00Z') },
      },
      bucket: 'DAY',
    });

    expect(screen.getByText('10 Nov 03:00 – 10 Nov 15:00 · 1 bucket.')).toBeInTheDocument();
  });

  it('disables the sizes the range does not allow, with their longest range', () => {
    renderBar({ ...defaultAnalyticsFilter, range: { kind: 'preset', preset: '31d' } });

    expect(bucketOption('Auto (1 day)')).toBeEnabled();
    expect(bucketOption('1 minute — ranges up to 16 h')).toBeDisabled();
    expect(bucketOption('5 minutes — ranges up to 3 d')).toBeDisabled();
    expect(bucketOption('15 minutes — ranges up to 10 d')).toBeDisabled();
    expect(bucketOption('1 hour')).toBeEnabled();
    expect(bucketOption('1 day')).toBeEnabled();
  });

  it('says when days start for DAY buckets', () => {
    renderBar({ ...defaultAnalyticsFilter, range: { kind: 'preset', preset: '31d' } });

    expect(screen.getByRole('combobox', { name: 'Bucket' })).toHaveAccessibleDescription(
      'Days start at 03:00 local time (00:00 UTC)',
    );
  });

  it('gives no hint for other buckets', () => {
    renderBar();

    expect(screen.getByRole('combobox', { name: 'Bucket' })).not.toHaveAttribute(
      'aria-describedby',
    );
  });

  it('applies a preset and resets a size the new range does not allow', async () => {
    const user = userEvent.setup();
    const onChange = renderBar({ ...defaultAnalyticsFilter, bucket: 'FIFTEEN_MINUTES' });

    await user.click(rangeButton('31 d'));

    expect(onChange).toHaveBeenCalledWith({
      range: { kind: 'preset', preset: '31d' },
      bucket: 'auto',
      location: null,
    });
  });

  it('applies a bucket size and back to Auto', async () => {
    const user = userEvent.setup();
    const onChange = renderBar();

    await user.selectOptions(screen.getByRole('combobox', { name: 'Bucket' }), '1 hour');
    expect(onChange).toHaveBeenLastCalledWith({ ...defaultAnalyticsFilter, bucket: 'HOUR' });

    await user.selectOptions(screen.getByRole('combobox', { name: 'Bucket' }), 'Auto (15 minutes)');
    expect(onChange).toHaveBeenLastCalledWith(defaultAnalyticsFilter);
  });

  it('applies a location and back to all locations', async () => {
    const user = userEvent.setup();
    const onChange = renderBar({ ...defaultAnalyticsFilter, location: 'GARAGE' });

    await user.selectOptions(screen.getByRole('combobox', { name: 'Locations' }), 'Living Room');
    expect(onChange).toHaveBeenLastCalledWith({
      ...defaultAnalyticsFilter,
      location: 'LIVING_ROOM',
    });

    await user.selectOptions(
      screen.getByRole('combobox', { name: 'Locations' }),
      'All locations (6)',
    );
    expect(onChange).toHaveBeenLastCalledWith(defaultAnalyticsFilter);
  });

  it('shows the gateway rejecting the filters', () => {
    renderBar(defaultAnalyticsFilter, true);

    expect(screen.getByRole('alert')).toHaveTextContent(INVALID_FILTER_MESSAGE);
  });

  describe('custom range', () => {
    async function openCustom() {
      const user = userEvent.setup();
      const onChange = renderBar();
      await user.click(rangeButton('Custom…'));
      return { user, onChange };
    }

    function setInputs(from: string, to: string) {
      fireEvent.change(screen.getByLabelText('From'), { target: { value: from } });
      fireEvent.change(screen.getByLabelText('To (exclusive)'), { target: { value: to } });
    }

    it('opens filled with the current range and sends nothing yet', async () => {
      const { onChange } = await openCustom();

      expect(rangeButton('Custom…')).toHaveAttribute('aria-pressed', 'true');
      expect(rangeButton('24 h')).toHaveAttribute('aria-pressed', 'false');
      expect(screen.getByLabelText('From')).toHaveValue('2026-11-14T14:30');
      expect(screen.getByLabelText('To (exclusive)')).toHaveValue('2026-11-15T14:30');
      expect(onChange).not.toHaveBeenCalled();
    });

    it('applies a valid range as local times', async () => {
      const { user, onChange } = await openCustom();

      setInputs('2026-11-10T09:00', '2026-11-12T09:00');
      await user.click(screen.getByRole('button', { name: 'Apply' }));

      expect(onChange).toHaveBeenCalledWith({
        ...defaultAnalyticsFilter,
        range: {
          kind: 'custom',
          range: { from: new Date('2026-11-10T06:00:00Z'), to: new Date('2026-11-12T06:00:00Z') },
        },
      });
    });

    it.each([
      ['an empty start', '', '2026-11-12T09:00', 'From', 'Enter a start and an end.'],
      ['an empty end', '2026-11-10T09:00', '', 'To (exclusive)', 'Enter a start and an end.'],
      [
        'an end before the start',
        '2026-11-12T09:00',
        '2026-11-10T09:00',
        'To (exclusive)',
        'The end must be later than the start.',
      ],
      [
        'an end equal to the start',
        '2026-11-12T09:00',
        '2026-11-12T09:00',
        'To (exclusive)',
        'The end must be later than the start.',
      ],
      [
        'a range over 31 days',
        '2026-10-01T09:00',
        '2026-11-10T09:00',
        'To (exclusive)',
        'Statistics cover up to 31 days. This range is 40 days.',
      ],
    ])('rejects %s at its field', async (_, from, to, field, message) => {
      const { user, onChange } = await openCustom();

      setInputs(from, to);
      await user.click(screen.getByRole('button', { name: 'Apply' }));

      expect(screen.getByLabelText(field)).toHaveAttribute('aria-invalid', 'true');
      expect(screen.getByLabelText(field)).toHaveAccessibleDescription(message);
      expect(onChange).not.toHaveBeenCalled();
    });

    it('accepts exactly 31 days', async () => {
      const { user, onChange } = await openCustom();

      setInputs('2026-10-10T09:00', '2026-11-10T09:00');
      await user.click(screen.getByRole('button', { name: 'Apply' }));

      expect(onChange).toHaveBeenCalledOnce();
    });

    it('closes when a preset is picked', async () => {
      const { user, onChange } = await openCustom();

      await user.click(rangeButton('24 h'));

      expect(screen.queryByRole('form', { name: 'Custom range' })).not.toBeInTheDocument();
      expect(onChange).toHaveBeenCalledWith(defaultAnalyticsFilter);
    });

    it('stays open for an applied custom range', () => {
      renderBar({
        ...defaultAnalyticsFilter,
        range: {
          kind: 'custom',
          range: { from: new Date('2026-11-01T06:00:00Z'), to: new Date('2026-11-08T06:00:00Z') },
        },
      });

      expect(rangeButton('Custom…')).toHaveAttribute('aria-pressed', 'true');
      expect(screen.getByLabelText('From')).toHaveValue('2026-11-01T09:00');
      expect(screen.getByLabelText('To (exclusive)')).toHaveValue('2026-11-08T09:00');
    });
  });
});
