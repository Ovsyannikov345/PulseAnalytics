import styles from './PageHeader.module.css';

interface PageHeaderProps {
  title: string;
  description: string;
}

/** The page's heading and introduction; also sets the document title. */
export function PageHeader({ title, description }: Readonly<PageHeaderProps>) {
  return (
    <div className={styles.header}>
      <title>{`${title} · PulseAnalytics`}</title>
      <h1 className={styles.title}>{title}</h1>
      <p className={styles.description}>{description}</p>
    </div>
  );
}
