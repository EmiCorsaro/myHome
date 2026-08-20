import { categoryTone } from "../lib/categoryTone";
import { cn } from "../lib/cn";
import { Money } from "./Money";

/** One row of a {@link CategoryBreakdown}. */
export interface CategoryBreakdownItem {
  /** Stable identifier. */
  id: string;
  /** Category name. */
  name: string;
  /** Tone from the expressive palette, 1 to 10, as published by the API. */
  colorIndex: number;
  /** Amount spent on the category. */
  total: number;
  /** Fraction of the period's total, from 0 to 1. Computed by the backend. */
  share: number;
}

/** Props for {@link CategoryBreakdown}. */
export interface CategoryBreakdownProps {
  /** Rows, in the order they should be read. Usually largest first. */
  items: readonly CategoryBreakdownItem[];
  /** Three-letter ISO 4217 currency code. `EUR` if omitted. */
  currency?: string;
  /** Locale used for the percentage. `es-ES` by default. */
  locale?: string;
  /** Extra classes for placement. */
  className?: string;
}

/**
 * Spending by category, as proportional bars.
 *
 * Bars and not a pie: people compare lengths against a shared baseline accurately and angles
 * badly, and a pie runs out of readable slices at about six categories.
 *
 * The share is never recomputed here — it arrives divided and rounded from the API, so these
 * percentages cannot disagree with the ones anywhere else.
 *
 * @param props - See {@link CategoryBreakdownProps}.
 * @returns The breakdown.
 *
 * @example
 * ```tsx
 * <CategoryBreakdown items={dashboard.byCategory} currency={dashboard.currency} />
 * ```
 */
export function CategoryBreakdown({
  items,
  currency = "EUR",
  locale = "es-ES",
  className,
}: CategoryBreakdownProps) {
  const percentage = new Intl.NumberFormat(locale, {
    style: "percent",
    maximumFractionDigits: 0,
  });

  return (
    <ul className={cn("flex flex-col gap-3", className)}>
      {items.map((item) => {
        const tone = categoryTone(item.colorIndex);

        return (
          <li key={item.id} className="flex flex-col gap-1.5">
            <div className="flex items-baseline justify-between gap-3">
              <span className="flex min-w-0 items-center gap-2">
                <span
                  className={cn("size-2.5 shrink-0 rounded-full", tone.background)}
                  aria-hidden="true"
                />
                <span className="truncate text-sm text-ink">{item.name}</span>
              </span>
              <span className="flex shrink-0 items-baseline gap-2">
                <Money
                  value={item.total}
                  currency={currency}
                  colorize={false}
                  className="text-sm"
                />
                <span className="w-9 text-right text-xs text-ink-faint tabular">
                  {percentage.format(item.share)}
                </span>
              </span>
            </div>

            {/* Decoration: the figure and percentage next to it already say everything. */}
            <div
              className="h-1.5 overflow-hidden rounded-full bg-surface-sunken"
              aria-hidden="true"
            >
              <div
                className={cn("h-full rounded-full", tone.background)}
                style={{ width: `${Math.min(100, Math.max(0, item.share * 100))}%` }}
              />
            </div>
          </li>
        );
      })}
    </ul>
  );
}
