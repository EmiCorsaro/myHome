import type { ButtonHTMLAttributes } from "react";
import { cn } from "../lib/cn";

/** How much emphasis a button carries. */
export type ButtonVariant = "primary" | "secondary" | "ghost";

/** Props for {@link Button}. */
export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  /**
   * Visual emphasis. At most one `primary` per screen: if everything stands out, nothing does.
   * `secondary` by default.
   */
  variant?: ButtonVariant;
}

const VARIANTS: Record<ButtonVariant, string> = {
  primary: "bg-accent text-white hover:opacity-90",
  secondary: "border border-line-strong bg-surface text-ink hover:bg-surface-sunken",
  ghost: "text-ink-soft hover:bg-surface-sunken hover:text-ink",
};

/**
 * The house button. Accepts everything a native `<button>` does; all it imposes is the look and
 * the focus ring.
 *
 * @param props - See {@link ButtonProps}.
 * @returns The button.
 *
 * @example
 * ```tsx
 * <Button variant="primary" onClick={startRound}>New round</Button>
 * <Button variant="ghost" type="button">Cancel</Button>
 * ```
 */
export function Button({ variant = "secondary", className, type, ...rest }: ButtonProps) {
  return (
    <button
      // Without an explicit type a button inside a form submits it, which is the usual cause of
      // "the page reloads when I click anything".
      type={type ?? "button"}
      className={cn(
        "inline-flex h-9 items-center justify-center gap-2 rounded-control px-3.5 text-sm font-medium",
        "transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent",
        "disabled:cursor-not-allowed disabled:opacity-50",
        VARIANTS[variant],
        className,
      )}
      {...rest}
    />
  );
}
