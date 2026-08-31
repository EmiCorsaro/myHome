import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiGet, apiPost } from "../../api/client";
import { DASHBOARD_QUERY_KEY, type AccountSummary } from "../dashboard/useDashboard";

/** A category the user can classify an expense as. */
export interface CategorySummary {
  /** Category identifier. */
  id: string;
  /** Visible name. */
  name: string;
  /** `income` or `expense`. */
  kind: string;
  /** Tone from the expressive palette, 1 to 10. */
  colorIndex: number;
  /** Parent category, or `null` at the top level. */
  parentId: string | null;
}

/** How often an expense repeats. `Once` creates no rule. */
export type IncomeRecurrence = "Once" | "Monthly" | "BiMonthly" | "Quarterly";

/** What the form sends when the user saves an income. */
export interface RegisterIncomeRequest {
  /** Account the money came into. */
  accountId: string;
  /** Category classifying the income. */
  categoryId: string;
  /** Amount received, positive. */
  amount: number;
  /** Date it happened, as `YYYY-MM-DD`. */
  occurredOn: string;
  /** What it was. */
  description: string;
  /** Whether it repeats, and how often. */
  recurrence: IncomeRecurrence;
  /**
   * Makes the request idempotent. Generated once per form, not per attempt, so pressing save
   * again after a timeout returns the income already recorded instead of adding a second one.
   */
  clientMutationId?: string;
}

/** The income as the API recorded it. */
export interface RegisteredIncome {
  /** Identifier of the created entry. */
  id: string;
  /** Date it happened. */
  occurredOn: string;
  /** What it was. */
  description: string;
  /** Amount received, positive. */
  amount: number;
  /** Three-letter ISO 4217 code. */
  currency: string;
  /** Account the money came into. */
  accountName: string;
  /** Category it was classified as. */
  categoryName: string;
  /** That category's tone. */
  categoryColorIndex: number;
  /** How often it repeats, as recorded. */
  recurrence: IncomeRecurrence;
  /** `true` when this request repeated one already saved and nothing new was created. */
  wasAlreadyRegistered: boolean;
}

/**
 * Loads the accounts an income can be received into.
 *
 * @returns The TanStack Query state for the request.
 */
export function useAccounts() {
  return useQuery({
    queryKey: ["accounts"],
    queryFn: () => apiGet<AccountSummary[]>("/api/accounts"),

    // Accounts and categories change a few times a year. No point re-requesting them every time
    // the form opens.
    staleTime: 5 * 60_000,
  });
}

/**
 * Loads the categories an income can be classified as.
 *
 * @returns The TanStack Query state for the request.
 */
export function useIncomeCategories() {
  return useQuery({
    queryKey: ["categories", "income"],
    queryFn: () => apiGet<CategorySummary[]>("/api/categories/income"),
    staleTime: 5 * 60_000,
  });
}

/**
 * Records an income.
 *
 * On success it invalidates the dashboard, so the totals, the breakdown and the movements list
 * all pick it up without any of them knowing a form exists.
 *
 * @returns The TanStack Mutation state.
 *
 * @example
 * ```tsx
 * const register = useRegisterIncome();
 *
 * register.mutate(
 *   { accountId, categoryId, amount: 42.35, occurredOn, description },
 *   { onSuccess: close },
 * );
 * ```
 */
export function useRegisterIncome() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: RegisterIncomeRequest) =>
      apiPost<RegisteredIncome>("/api/income", request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: DASHBOARD_QUERY_KEY }),
  });
}
