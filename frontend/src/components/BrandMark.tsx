interface BrandMarkProps {
  size: number;
}

/** The PulseAnalytics logo: a pulse line on a rounded square. Decorative, so hidden from screen readers. */
export function BrandMark({ size }: Readonly<BrandMarkProps>) {
  return (
    <svg aria-hidden="true" width={size} height={size} viewBox="0 0 28 28">
      <rect width="28" height="28" rx="7" fill="var(--color-primary)" />
      <path
        d="M5 15h4l2.5-6 4 11 2.5-5H23"
        fill="none"
        stroke="var(--color-text-inverse)"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
