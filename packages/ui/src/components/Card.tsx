import type { ReactNode } from "react";
import { cn } from "../lib/cn";

/** Props for {@link Card}. */
export interface CardProps {
  /** Card content. */
  children: ReactNode;
  /** Extra classes for placement — column span, ordering. Not for changing the surface. */
  className?: string;
}

/**
 * The raised surface everything on a screen sits on. One border, one radius, one padding, chosen
 * once so two screens built on different days still line up.
 *
 * @param props - See {@link CardProps}.
 * @returns The card.
 *
 * @example
 * ```tsx
 * <Card>
 *   <h3 className="text-sm text-ink-soft">Balance</h3>
 *   <Money value={2530} />
 * </Card>
 * ```
 */
export function Card({ children, className }: CardProps) {
  return (
    <div className={cn("rounded-card border border-line bg-surface p-4 sm:p-5", className)}>
      {children}
    </div>
  );
}
