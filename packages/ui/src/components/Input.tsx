import type { InputHTMLAttributes, SelectHTMLAttributes } from "react";
import { useFieldState } from "./Field";
import { cn } from "../lib/cn";

/** Shared look, so an input and a select side by side are the same height and share a border. */
const CONTROL_CLASSES = [
  "h-10 w-full rounded-control border bg-surface px-3 text-sm text-ink",
  "transition-colors placeholder:text-ink-faint",
  "focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent",
  "disabled:cursor-not-allowed disabled:bg-surface-sunken disabled:text-ink-faint",
].join(" ");

/** Props for {@link Input}. Everything a native `<input>` accepts. */
export type InputProps = InputHTMLAttributes<HTMLInputElement>;

/**
 * The house text input. Inside a {@link Field} it wires itself up: takes the label's `id`, points
 * at the error message, marks itself invalid. Outside one it is a plain styled input.
 *
 * @param props - See {@link InputProps}.
 * @returns The input.
 *
 * @example
 * ```tsx
 * <Field label="Description" error={errors.description} required>
 *   <Input value={description} onChange={(e) => setDescription(e.target.value)} />
 * </Field>
 * ```
 */
export function Input({ className, id, ...rest }: InputProps) {
  const field = useFieldState();

  return (
    <input
      id={id ?? field?.controlId}
      aria-describedby={field?.describedBy}
      aria-invalid={field?.invalid ? true : undefined}
      className={cn(
        CONTROL_CLASSES,
        field?.invalid ? "border-negative" : "border-line-strong",
        className,
      )}
      {...rest}
    />
  );
}

/** Props for {@link Select}. Everything a native `<select>` accepts. */
export type SelectProps = SelectHTMLAttributes<HTMLSelectElement>;

/**
 * The house select. A native `<select>`: a custom dropdown would have to reimplement keyboard
 * navigation, type-ahead and the mobile picker, and be worse at all three.
 *
 * @param props - See {@link SelectProps}.
 * @returns The select.
 *
 * @example
 * ```tsx
 * <Field label="Account" required>
 *   <Select value={accountId} onChange={(e) => setAccountId(e.target.value)}>
 *     {accounts.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
 *   </Select>
 * </Field>
 * ```
 */
export function Select({ className, id, ...rest }: SelectProps) {
  const field = useFieldState();

  return (
    <select
      id={id ?? field?.controlId}
      aria-describedby={field?.describedBy}
      aria-invalid={field?.invalid ? true : undefined}
      className={cn(
        CONTROL_CLASSES,
        field?.invalid ? "border-negative" : "border-line-strong",
        className,
      )}
      {...rest}
    />
  );
}
