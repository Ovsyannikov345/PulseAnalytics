import { PageHeader } from '../../components/PageHeader';

export function AnalyticsPage() {
  return (
    <PageHeader
      title="Analytics"
      description="Average, minimum and maximum per location and time bucket. Buckets sit on a fixed UTC grid, so they don't move as the range follows the current time."
    />
  );
}
