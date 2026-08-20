import { useQuery } from "@tanstack/react-query";
import { apiGet } from "../../api/client";

/** A household member, as returned by the API. */
export interface HouseholdMember {
  /** Member identifier. */
  id: string;
  /** Visible name. */
  displayName: string;
  /** Role within the household: `Owner`, `Member` or `Viewer`. */
  role: string;
  /** Whether the member has a sign-in account. */
  hasAccount: boolean;
}

/** The current session's household, as returned by the API. */
export interface Household {
  /** Household identifier. */
  id: string;
  /** Household name. */
  name: string;
  /** Base currency, as a three-letter ISO 4217 code. */
  baseCurrency: string;
  /** The household's IANA time zone. */
  timeZoneId: string;
  /** Members, in display order. */
  members: HouseholdMember[];
}

/**
 * Loads the current session's household.
 *
 * @returns The TanStack Query state for the request.
 *
 * @example
 * ```tsx
 * const { data, isPending, error } = useHousehold();
 * if (isPending) return <Skeleton />;
 * ```
 */
export function useHousehold() {
  return useQuery({
    queryKey: ["household"],
    queryFn: () => apiGet<Household>("/api/household"),
  });
}
