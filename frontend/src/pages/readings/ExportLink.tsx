import { Button } from '../../components/Button';
import { ButtonLink } from '../../components/ButtonLink';
import { exportUrl, type ReadingsFilter } from './readingsFilter';

function DownloadIcon() {
  return (
    <svg aria-hidden="true" width="16" height="16" viewBox="0 0 16 16">
      <path
        d="M8 2v8M4.5 6.5 8 10l3.5-3.5M2.5 13.5h11"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

interface ExportLinkProps {
  /** The applied filter. */
  filter: ReadingsFilter;
  /** Why the filter can't be exported, or `null`. */
  problem: string | null;
  /** The id of the text that explains the export, or the problem. */
  describedBy: string;
}

/**
 * Downloads the filter's readings as CSV. A plain link: the browser streams the file to disk and shows
 * its progress, which a page can't observe. Over the export's limit it is a disabled button instead.
 */
export function ExportLink({ filter, problem, describedBy }: Readonly<ExportLinkProps>) {
  if (problem) {
    return (
      <Button variant="outline" disabled aria-describedby={describedBy}>
        <DownloadIcon />
        Export CSV
      </Button>
    );
  }

  return (
    <ButtonLink variant="outline" href={exportUrl(filter)} download aria-describedby={describedBy}>
      <DownloadIcon />
      Export CSV
    </ButtonLink>
  );
}
