import { useQuery } from "@tanstack/react-query";
import { apiGet } from "../../api/client";

/** What was spent on one category during the period. */
export interface CategoryTotal {
  /** Category identifier. */
  categoryId: string;
  /** Visible name. */
  name: string;
  /** Tone from the expressive palette, 1 to 10. */
  colorIndex: number;
  /** Amount spent, positive. */
  total: number;
  /** Fraction of the period's total expense, from 0 to 1. Already computed by the API. */
  share: number;
}

/** An account holding real money. */
export interface AccountSummary {
  /** Account identifier. */
  id: string;
  /** Visible name. */
  name: string;
  /** `checking`, `savings`, `cash` or `creditCard`. */
  type: string;
  /** Three-letter ISO 4217 code. */
  currency: string;
  /** Current balance. */
  balance: number;
  /** Whether the account takes part in balance projection. */
  isTracked: boolean;
  /** Balance floor, or `null` if none has been set. */
  minimumBufferTarget: number | null;
}

/** One movement, flattened for a listing. */
export interface LedgerEntrySummary {
  /** Entry identifier. */
  id: string;
  /** Date it happened, as `YYYY-MM-DD`. */
  occurredOn: string;
  /** What it was. */
  description: string;
  /** `income`, `expense`, `transfer` or `opening`. */
  kind: string;
  /** Signed amount: negative for money leaving. */
  amount: number;
  /** Real account affected. */
  accountName: string;
  /** Category, when the entry has one. */
  categoryName: string | null;
  /** That category's tone, when there is one. */
  categoryColorIndex: number | null;
  /** Whether the movement comes from a recurring rule. */
  isRecurring: boolean;
}

/** Everything the landing screen shows. */
export interface DashboardSummary {
  /** The household's base currency. */
  currency: string;
  /** First day of the period, as `YYYY-MM-DD`. */
  periodStart: string;
  /** Last day of the period, inclusive. */
  periodEnd: string;
  /** Money that came in during the period. */
  income: number;
  /** Money that went out during the period. */
  expense: number;
  /** Income minus expense. Negative means the household spent more than it earned. */
  net: number;
  /** Money available right now across tracked accounts. */
  trackedBalance: number;
  /** Whether a forward-looking projection can be computed yet. */
  isProjectionAvailable: boolean;
  /** Expense by category, largest first. */
  byCategory: CategoryTotal[];
  /** Accounts with their balances. */
  accounts: AccountSummary[];
  /** The most recent movements, newest first. */
  recentEntries: LedgerEntrySummary[];
}

/**
 * Root of the dashboard query keys. Each month caches under its own key below this one, so
 * invalidating the root refreshes every month the user has visited.
 */
export const DASHBOARD_QUERY_KEY = ["dashboard"] as const;

/**
 * Loads the dashboard for one month.
 *
 * @param month - Any day of the month to show, as `YYYY-MM-DD`. Defaults to the current month.
 * @returns The TanStack Query state for the request.
 *
 * @example
 * ```tsx
 * const { data, isPending, error } = useDashboard("2026-08-01");
 * ```
 */
export function useDashboard(month?: string) {
  return useQuery({
    queryKey: [...DASHBOARD_QUERY_KEY, month ?? "current"],
    queryFn: () =>
      apiGet<DashboardSummary>(month ? `/api/dashboard?month=${month}` : "/api/dashboard"),

    // Keeps the previous month on screen while the next loads, so stepping through months does
    // not flash an empty layout on every click.
    placeholderData: (previous) => previous,
  });
}
