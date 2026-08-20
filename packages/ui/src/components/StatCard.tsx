import type { ReactNode } from "react";
import { Card } from "./Card";
import { Money } from "./Money";
import { cn } from "../lib/cn";

/** Props for {@link StatCard}. */
export interface StatCardProps {
  /** What the figure is. Kept short: "Income", "Spent", "Balance today". */
  label: string;
  /** The amount, in major currency units. */
  value: number;
  /** Three-letter ISO 4217 currency code. `EUR` if omitted. */
  currency?: string;
  /**
   * Tints the amount by its sign. Off by default: most dashboard figures are neutral facts, and a
   * wall of green and red hides the one figure that is actually a warning.
   */
  colorize?: boolean;
  /** A line under the amount: what it covers, when it was measured, why it is worth reading. */
  hint?: ReactNode;
  /** Small mark shown before the label — a dot, an icon. */
  badge?: ReactNode;
  /** Extra classes for placement. */
  className?: string;
}

/**
 * A headline figure with its label. Answers "how big should this number be" once, instead of on
 * every screen.
 *
 * @param props - See {@link StatCardProps}.
 * @returns The card with its figure.
 *
 * @example
 * ```tsx
 * <StatCard label="Spent" value={1842.5} hint="August, so far" />
 * <StatCard label="Balance today" value={2530} colorize hint="Tracked accounts" />
 * ```
 */
export function StatCard({
  label,
  value,
  currency = "EUR",
  colorize = false,
  hint,
  badge,
  className,
}: StatCardProps) {
  return (
    <Card className={cn("flex flex-col gap-1", className)}>
      <div className="flex items-center gap-2">
        {badge}
        <span className="text-sm font-medium text-ink-soft">{label}</span>
      </div>
      <Money
        value={value}
        currency={currency}
        colorize={colorize}
        className="text-2xl font-semibold"
      />
      {hint ? <p className="text-xs text-ink-faint">{hint}</p> : null}
    </Card>
  );
}
