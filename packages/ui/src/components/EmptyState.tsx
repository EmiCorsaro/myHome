import type { ReactNode } from "react";
import { cn } from "../lib/cn";

/** Props for {@link EmptyState}. */
export interface EmptyStateProps {
  /** What is missing, in one line: "No expenses in August yet". */
  title: string;
  /** Why it is empty, or what to do about it. */
  description?: ReactNode;
  /** The action that fills it: usually a single button. */
  action?: ReactNode;
  /** Extra classes for placement. */
  className?: string;
}

/**
 * What a section shows when it has nothing to show.
 *
 * A first-run screen is almost all empty states, so they are the first impression rather than an
 * edge case. Having a component means the question gets answered every time, the same way.
 *
 * @param props - See {@link EmptyStateProps}.
 * @returns The empty state.
 *
 * @example
 * ```tsx
 * <EmptyState
 *   title="No movements yet"
 *   description="Record the first expense and it will appear here."
 *   action={<Button variant="primary" onClick={openForm}>Add expense</Button>}
 * />
 * ```
 */
export function EmptyState({ title, description, action, className }: EmptyStateProps) {
  return (
    <div className={cn("flex flex-col items-center gap-2 px-4 py-8 text-center", className)}>
      <p className="text-sm font-medium text-ink">{title}</p>
      {description ? <p className="max-w-sm text-sm text-ink-soft">{description}</p> : null}
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  );
}
