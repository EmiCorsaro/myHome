import type { ReactNode } from "react";
import { cn } from "../lib/cn";

/** Props for {@link Section}. */
export interface SectionProps {
  /** Section title. */
  title: string;
  /** Supporting line under the title. Optional. */
  description?: string;
  /** Actions aligned to the right of the title: buttons, filters, links. */
  actions?: ReactNode;
  /** Section content. */
  children: ReactNode;
  /** Extra classes for placement. */
  className?: string;
}

/**
 * A content block with a header.
 *
 * Exists so nobody hand-tunes margins. The vertical rhythm lives here and nowhere else: once a
 * screen starts dropping a loose `mt-6` there is no single place left to fix it.
 *
 * @param props - See {@link SectionProps}.
 * @returns The section with its header.
 *
 * @example
 * ```tsx
 * <Section
 *   title="September entries"
 *   description="Main account"
 *   actions={<Button>New round</Button>}
 * >
 *   <DataTable columns={columns} rows={entries} />
 * </Section>
 * ```
 */
export function Section({ title, description, actions, children, className }: SectionProps) {
  return (
    <section className={cn("flex flex-col gap-4", className)}>
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="text-lg font-medium text-ink">{title}</h2>
          {description ? <p className="mt-0.5 text-sm text-ink-soft">{description}</p> : null}
        </div>
        {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
      </header>
      {children}
    </section>
  );
}
