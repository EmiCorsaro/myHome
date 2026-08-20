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
export type ExpenseRecurrence = "Once" | "Monthly" | "BiMonthly" | "Quarterly";

/** What the form sends when the user saves an expense. */
export interface RegisterExpenseRequest {
  /** Account the money came out of. */
  accountId: string;
  /** Category classifying the expense. */
  categoryId: string;
  /** Amount spent, positive. */
  amount: number;
  /** Date it happened, as `YYYY-MM-DD`. */
  occurredOn: string;
  /** What it was. */
  description: string;
  /** Whether it repeats, and how often. */
  recurrence: ExpenseRecurrence;
  /**
   * Makes the request idempotent. Generated once per form, not per attempt, so pressing save
   * again after a timeout returns the expense already recorded instead of adding a second one.
   */
  clientMutationId?: string;
}

/** The expense as the API recorded it. */
export interface RegisteredExpense {
  /** Identifier of the created entry. */
  id: string;
  /** Date it happened. */
  occurredOn: string;
  /** What it was. */
  description: string;
  /** Amount spent, positive. */
  amount: number;
  /** Three-letter ISO 4217 code. */
  currency: string;
  /** Account the money left. */
  accountName: string;
  /** Category it was classified as. */
  categoryName: string;
  /** That category's tone. */
  categoryColorIndex: number;
  /** How often it repeats, as recorded. */
  recurrence: ExpenseRecurrence;
  /** `true` when this request repeated one already saved and nothing new was created. */
  wasAlreadyRegistered: boolean;
}

/**
 * Loads the accounts an expense can be paid from.
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
 * Loads the categories an expense can be classified as.
 *
 * @returns The TanStack Query state for the request.
 */
export function useExpenseCategories() {
  return useQuery({
    queryKey: ["categories", "expense"],
    queryFn: () => apiGet<CategorySummary[]>("/api/categories/expense"),
    staleTime: 5 * 60_000,
  });
}

/**
 * Records an expense.
 *
 * On success it invalidates the dashboard, so the totals, the breakdown and the movements list
 * all pick it up without any of them knowing a form exists.
 *
 * @returns The TanStack Mutation state.
 *
 * @example
 * ```tsx
 * const register = useRegisterExpense();
 *
 * register.mutate(
 *   { accountId, categoryId, amount: 42.35, occurredOn, description },
 *   { onSuccess: close },
 * );
 * ```
 */
export function useRegisterExpense() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: RegisterExpenseRequest) =>
      apiPost<RegisteredExpense>("/api/expenses", request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: DASHBOARD_QUERY_KEY }),
  });
}
