import { cn } from "../lib/cn";

/** How the sign of the amount is displayed. */
export type MoneySignDisplay = "auto" | "always" | "never";

/** Props for {@link Money}. */
export interface MoneyProps {
  /** Amount in major units of the currency (euros, not cents). Negative means money leaving. */
  value: number;
  /** Three-letter ISO 4217 currency code. `EUR` if omitted. */
  currency?: string;
  /**
   * Tints the amount by its sign: green for income, red for expense. Turn it off on neutral
   * totals, where the colour says nothing. `true` by default.
   */
  colorize?: boolean;
  /**
   * `auto` shows the sign only on negatives, `always` shows it on positives too, `never` omits
   * it (useful when the column already says whether it is a debit or a credit). `auto` by
   * default.
   */
  signDisplay?: MoneySignDisplay;
  /** Locale used for formatting. `es-ES` by default. */
  locale?: string;
  /** Extra classes for placement. Not for changing how the amount looks. */
  className?: string;
}

/**
 * Renders a money amount. Every figure in the product goes through here.
 *
 * A loose amount in a `<span>` ends up with a different separator on every screen and columns that
 * do not line up. One component means two people working apart cannot paint two amounts
 * differently.
 *
 * Tabular figures are always on. Without them digits have different widths and a column of amounts
 * stops being readable at a glance.
 *
 * @param props - See {@link MoneyProps}.
 * @returns The formatted amount.
 *
 * @example Ordinary amount, tinted by its sign
 * ```tsx
 * <Money value={-1250} />        // −1.250,00 € in red
 * <Money value={3510} />         //  3.510,00 € in green
 * ```
 *
 * @example A neutral total, where colour adds nothing
 * ```tsx
 * <Money value={balance} colorize={false} />
 * ```
 *
 * @example Inside a numeric table column
 * ```tsx
 * { key: "amount", header: "Amount", align: "end",
 *   render: (v: number) => <Money value={v} /> }
 * ```
 */
export function Money({
  value,
  currency = "EUR",
  colorize = true,
  signDisplay = "auto",
  locale = "es-ES",
  className,
}: MoneyProps) {
  const formatted = new Intl.NumberFormat(locale, {
    style: "currency",
    currency,
    signDisplay: signDisplay === "never" ? "never" : signDisplay,
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(value);

  const tone = !colorize
    ? "text-ink"
    : value < 0
      ? "text-negative"
      : value > 0
        ? "text-positive"
        : "text-ink-soft";

  return <span className={cn("tabular", tone, className)}>{formatted}</span>;
}
