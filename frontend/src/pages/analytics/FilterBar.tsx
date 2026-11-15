import { useId, useState } from 'react';

import { Field } from '../../components/Field';
import { SegmentedControl, type SegmentedOption } from '../../components/SegmentedControl';
import { Select } from '../../components/Select';
import {
  formatDateTime,
  formatDayStartHint,
  formatNumber,
  formatRangeLimit,
} from '../../domain/formatting';
import { meterLocationLabel, meterLocations } from '../../domain/meters';
import {
  autoBucket,
  bucketGrid,
  isBucketAllowed,
  MAX_BUCKETS,
  maxRangeMs,
  timeBucketLabel,
  timeBuckets,
} from '../../domain/timeBuckets';
import {
  rangePresetLabel,
  rangePresets,
  type RangePreset,
  type TimeRange,
} from '../../domain/timeRanges';
import { getUtcOffsetMinutes } from '../../domain/utcOffset';
import type { TimeBucket } from '../../graphql/generated/enums';
import { INVALID_FILTER_MESSAGE } from '../../graphql/errors';
import { withRange, type AnalyticsFilter } from './analyticsFilter';
import { CustomRangeForm } from './CustomRangeForm';
import styles from './FilterBar.module.css';

type RangeOption = RangePreset | 'custom';

const rangeOptions: readonly SegmentedOption<RangeOption>[] = [
  ...rangePresets.map((preset) => ({ value: preset, label: rangePresetLabel(preset) })),
  { value: 'custom', label: 'Custom…' },
];

function bucketOptionLabel(bucket: TimeBucket, allowed: boolean): string {
  const label = timeBucketLabel(bucket);
  return allowed ? label : `${label} — ranges up to ${formatRangeLimit(maxRangeMs(bucket))}`;
}

interface FilterBarProps {
  filter: AnalyticsFilter;
  /** The applied range, resolved at the time the filter was applied. */
  range: TimeRange;
  /** The bucket the queries use: the selected size or Auto's choice. */
  bucket: TimeBucket;
  /** The gateway rejected the filters (a fallback: the form checks them first). */
  invalidInput: boolean;
  onChange: (filter: AnalyticsFilter) => void;
}

export function FilterBar({
  filter,
  range,
  bucket,
  invalidInput,
  onChange,
}: Readonly<FilterBarProps>) {
  const rangeLabelId = useId();
  const [customOpen, setCustomOpen] = useState(false);

  const custom = filter.range.kind === 'custom';
  const pressedRange: RangeOption =
    filter.range.kind === 'custom' || customOpen ? 'custom' : filter.range.preset;
  const allowedBuckets = timeBuckets.filter((size) => isBucketAllowed(size, range));
  const bucketCount = bucketGrid(range, bucket).length;

  function selectRange(option: RangeOption) {
    if (option === 'custom') {
      setCustomOpen(true);
      return;
    }
    setCustomOpen(false);
    onChange(withRange(filter, { kind: 'preset', preset: option }));
  }

  return (
    <section aria-label="Filters" className={styles.bar}>
      <div className={styles.controls}>
        <div className={styles.rangeGroup}>
          <span id={rangeLabelId} className={styles.groupLabel}>
            Time range
          </span>
          <SegmentedControl
            labelledBy={rangeLabelId}
            options={rangeOptions}
            value={pressedRange}
            onChange={selectRange}
          />
        </div>
        <Field
          label="Bucket"
          hint={bucket === 'DAY' ? formatDayStartHint(getUtcOffsetMinutes(range.to)) : null}
        >
          {(control) => (
            <Select
              {...control}
              value={filter.bucket}
              onChange={(event) => {
                onChange({
                  ...filter,
                  bucket: timeBuckets.find((size) => size === event.target.value) ?? 'auto',
                });
              }}
            >
              <option value="auto">Auto ({timeBucketLabel(autoBucket(range))})</option>
              {timeBuckets.map((size) => {
                const allowed = allowedBuckets.includes(size);
                return (
                  <option key={size} value={size} disabled={!allowed}>
                    {bucketOptionLabel(size, allowed)}
                  </option>
                );
              })}
            </Select>
          )}
        </Field>
        <Field label="Locations">
          {(control) => (
            <Select
              {...control}
              value={filter.location ?? ''}
              onChange={(event) => {
                onChange({
                  ...filter,
                  location: meterLocations.find((value) => value === event.target.value) ?? null,
                });
              }}
            >
              <option value="">All locations ({formatNumber(meterLocations.length)})</option>
              {meterLocations.map((location) => (
                <option key={location} value={location}>
                  {meterLocationLabel(location)}
                </option>
              ))}
            </Select>
          )}
        </Field>
        <p className={styles.summary}>
          {formatDateTime(range.from)} – {formatDateTime(range.to)} · {formatNumber(bucketCount)}{' '}
          {bucketCount === 1 ? 'bucket' : 'buckets'}.
          {allowedBuckets.length < timeBuckets.length
            ? ` Bucket sizes that would need more than ${formatNumber(MAX_BUCKETS)} buckets for this range can't be picked.`
            : null}
        </p>
      </div>
      {custom || customOpen ? (
        <CustomRangeForm
          // A newly applied range (or one from the back button) refills the inputs.
          key={custom ? `${String(range.from.getTime())}-${String(range.to.getTime())}` : 'preset'}
          initialRange={range}
          onApply={(customRange) => {
            setCustomOpen(false);
            onChange(withRange(filter, { kind: 'custom', range: customRange }));
          }}
        />
      ) : null}
      {invalidInput ? (
        <p role="alert" className={styles.alert}>
          {INVALID_FILTER_MESSAGE}
        </p>
      ) : null}
    </section>
  );
}
