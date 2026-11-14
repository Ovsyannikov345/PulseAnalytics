import { Link } from 'react-router';

import { PageHeader } from '../../components/PageHeader';

export function NotFoundPage() {
  return (
    <>
      <PageHeader title="Page not found" description="There is no page at this address." />
      <p>
        <Link to="/">Go to the overview</Link>
      </p>
    </>
  );
}
