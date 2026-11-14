import { Link, NavLink, Outlet } from 'react-router';

import { BrandMark } from '../components/BrandMark';
import { cx } from '../components/cx';
import { formatUtcOffset, getUtcOffsetMinutes } from '../domain/utcOffset';
import styles from './AppLayout.module.css';

const navItems = [
  { to: '/', label: 'Overview', end: true },
  { to: '/analytics', label: 'Analytics', end: false },
  { to: '/readings', label: 'Readings', end: false },
] as const;

function navLinkClassName({ isActive }: { isActive: boolean }): string {
  return cx(styles.navLink, isActive && styles.navLinkActive);
}

/** The page frame: header with brand, main navigation and the local time zone, then the page. */
export function AppLayout() {
  const utcOffset = formatUtcOffset(getUtcOffsetMinutes());

  return (
    <>
      <a className={styles.skipLink} href="#content">
        Skip to content
      </a>
      <header className={styles.header}>
        <div className={styles.headerInner}>
          <Link className={styles.brand} to="/">
            <BrandMark size={28} />
            <span>PulseAnalytics</span>
          </Link>
          <nav className={styles.nav} aria-label="Main">
            {navItems.map((item) => (
              <NavLink key={item.to} to={item.to} end={item.end} className={navLinkClassName}>
                {item.label}
              </NavLink>
            ))}
          </nav>
          <span className={styles.timeZone}>
            <span className={styles.timeZoneLabel}>Local time (</span>
            {utcOffset}
            <span className={styles.timeZoneLabel}>)</span>
          </span>
        </div>
      </header>
      <main id="content" className={styles.main} tabIndex={-1}>
        <Outlet />
      </main>
    </>
  );
}
