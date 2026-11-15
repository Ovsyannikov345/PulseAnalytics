import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  defaultReadingsFilter,
  orderTypes,
  parseReadingsFilter,
  readDraft,
  readingFilterInput,
  toDraft,
  toSearchParams,
  type ReadingsDraft,
  type ReadingsFilter,
} from './readingsFilter';

// 14:29:47 in Moscow.
const now = new Date('2026-11-15T11:29:47Z');

const lastHour = {
  from: new Date('2026-11-15T10:30:00Z'),
  to: new Date('2026-11-15T11:30:00Z'),
};

const allTypes = ['AIR_QUALITY', 'ENERGY', 'MOTION'];

function parse(search: string): ReadingsFilter {
  return parseReadingsFilter(new URLSearchParams(search), now);
}

describe('defaultReadingsFilter', () => {
  it('is every meter over the hour up to the next whole minute', () => {
    expect(defaultReadingsFilter(now)).toEqual({
      types: allTypes,
      location: null,
      range: lastHour,
    });
  });

  it('ends at the current minute when the time is on it', () => {
    expect(defaultReadingsFilter(new Date('2026-11-15T11:30:00Z')).range).toEqual(lastHour);
  });
});

describe('orderTypes', () => {
  it('keeps known types once, in the meter types order', () => {
    expect(orderTypes(['MOTION', 'WATER', 'AIR_QUALITY', 'MOTION'])).toEqual([
      'AIR_QUALITY',
      'MOTION',
    ]);
  });
});

describe('parseReadingsFilter', () => {
  it('defaults to every meter over the last hour', () => {
    expect(parse('')).toEqual(defaultReadingsFilter(now));
  });

  it('reads types, a location and a range', () => {
    expect(
      parse(
        'from=2026-11-01T06:00:00.000Z&to=2026-11-08T06:00:00.000Z&type=MOTION&type=ENERGY&location=GARAGE',
      ),
    ).toEqual({
      types: ['ENERGY', 'MOTION'],
      location: 'GARAGE',
      range: { from: new Date('2026-11-01T06:00:00Z'), to: new Date('2026-11-08T06:00:00Z') },
    });
  });

  it.each([
    ['unknown types', 'type=WATER', { types: allTypes }],
    ['an unknown location', 'location=ATTIC', { location: null }],
    ['a range without a start', 'to=2026-11-01T06:00:00Z', { range: lastHour }],
    ['a range without an end', 'from=2026-11-01T06:00:00Z', { range: lastHour }],
    ['an unparseable range', 'from=yesterday&to=today', { range: lastHour }],
    ['a range that ends before it starts', 'from=2026-11-08&to=2026-11-01', { range: lastHour }],
    ['an empty range', 'from=2026-11-01&to=2026-11-01', { range: lastHour }],
  ])('falls back to the default for %s', (_case, search, expected) => {
    expect(parse(search)).toEqual({ ...defaultReadingsFilter(now), ...expected });
  });
});

describe('toSearchParams', () => {
  it('leaves out all types and all locations', () => {
    expect(toSearchParams(defaultReadingsFilter(now)).toString()).toBe(
      'from=2026-11-15T10%3A30%3A00.000Z&to=2026-11-15T11%3A30%3A00.000Z',
    );
  });

  it('repeats the type parameter and round-trips', () => {
    const filter: ReadingsFilter = {
      types: ['AIR_QUALITY', 'MOTION'],
      location: 'LIVING_ROOM',
      range: lastHour,
    };
    const params = toSearchParams(filter);

    expect(params.getAll('type')).toEqual(['AIR_QUALITY', 'MOTION']);
    expect(params.get('location')).toBe('LIVING_ROOM');
    expect(parseReadingsFilter(params, now)).toEqual(filter);
  });
});

describe('readingFilterInput', () => {
  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  it('sends local times with their offset and no locations for all', () => {
    expect(readingFilterInput(defaultReadingsFilter(now))).toEqual({
      types: allTypes,
      locations: [],
      from: '2026-11-15T13:30:00.000+03:00',
      to: '2026-11-15T14:30:00.000+03:00',
    });
  });

  it('sends the selected location', () => {
    expect(
      readingFilterInput({ types: ['ENERGY'], location: 'KITCHEN', range: lastHour }),
    ).toMatchObject({ types: ['ENERGY'], locations: ['KITCHEN'] });
  });
});

describe('readDraft', () => {
  const draft: ReadingsDraft = {
    types: ['MOTION', 'ENERGY'],
    location: 'OFFICE',
    from: '2026-11-15T13:30',
    to: '2026-11-15T14:30',
  };

  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  it('fills the form with local minutes', () => {
    expect(toDraft(defaultReadingsFilter(now))).toEqual({
      types: allTypes,
      location: null,
      from: '2026-11-15T13:30',
      to: '2026-11-15T14:30',
    });
  });

  it('reads valid values as local time, with ordered types', () => {
    expect(readDraft(draft)).toEqual({
      filter: { types: ['ENERGY', 'MOTION'], location: 'OFFICE', range: lastHour },
      problems: null,
    });
  });

  it.each([
    ['no type', { types: [] }, { types: 'Pick at least one meter type.' }],
    ['no start', { from: '' }, { from: 'Enter a start and an end.' }],
    ['no end', { to: '' }, { to: 'Enter a start and an end.' }],
    ['no start and no end', { from: '', to: '' }, { from: 'Enter a start and an end.' }],
    [
      'an end before the start',
      { to: '2026-11-15T13:00' },
      { to: 'The end must be later than the start.' },
    ],
    ['an empty range', { to: '2026-11-15T13:30' }, { to: 'The end must be later than the start.' }],
  ])('reports %s', (_case, values, problems) => {
    expect(readDraft({ ...draft, ...values })).toEqual({ filter: null, problems });
  });

  it('reports every problem at once', () => {
    expect(readDraft({ ...draft, types: [], to: '' }).problems).toEqual({
      types: 'Pick at least one meter type.',
      to: 'Enter a start and an end.',
    });
  });
});
