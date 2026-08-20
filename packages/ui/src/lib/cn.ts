import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

/**
 * Combines Tailwind classes, resolving conflicts in favour of the last one.
 *
 * Without it `cn("p-2", "p-4")` leaves both in the attribute and whichever the generated CSS
 * emits last wins, which is not predictable. With it `p-4` wins, which is what a caller passing an
 * override expects.
 *
 * @param inputs - Classes, conditionals or objects, exactly as in `clsx`.
 * @returns The resolved class string.
 *
 * @example
 * ```tsx
 * <div className={cn("rounded-card p-4", isActive && "bg-accent-bg", className)} />
 * ```
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
