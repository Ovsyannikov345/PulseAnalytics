import type { ReactNode } from 'react';

import type { MeterType } from '../../graphql/generated/enums';

const paths: Record<MeterType, ReactNode> = {
  AIR_QUALITY: (
    <path
      d="M3 6c2-1.5 4 1.5 6 0s3-1 4 0M3 10c2-1.5 4 1.5 6 0s3-1 4 0"
      fill="none"
      strokeLinecap="round"
    />
  ),
  ENERGY: <path d="M9 1.5 3.5 9H8l-1 5.5L12.5 7H8Z" fill="none" strokeLinejoin="round" />,
  MOTION: (
    <>
      <circle cx="8" cy="8" r="2" fill="currentColor" stroke="none" />
      <path d="M4.5 4.5a5 5 0 0 0 0 7M11.5 4.5a5 5 0 0 1 0 7" fill="none" strokeLinecap="round" />
    </>
  ),
};

/** The meter type's icon from the Readings board (waves, a bolt, a sensor); decorative. */
export function MeterTypeIcon({ type }: Readonly<{ type: MeterType }>) {
  return (
    <svg
      aria-hidden="true"
      width="16"
      height="16"
      viewBox="0 0 16 16"
      stroke="currentColor"
      strokeWidth="1.5"
    >
      {paths[type]}
    </svg>
  );
}
