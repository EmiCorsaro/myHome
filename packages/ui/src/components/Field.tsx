import { createContext, useContext, useId, type ReactNode } from "react";
import { cn } from "../lib/cn";

/** What a field tells the control inside it. */
interface FieldState {
  /** Identifier of the control, matching the label's `htmlFor`. */
  controlId: string;
  /** Identifier of the element describing the control: hint or error. */
  describedBy: string | undefined;
  /** Whether the field is currently in error. */
  invalid: boolean;
}

const FieldContext = createContext<FieldState | null>(null);

/**
 * Reads the surrounding field, if there is one.
 *
 * Used by {@link Input} and {@link Select} to find their label, hint and error without the caller
 * passing a single `id`.
 *
 * @returns The field state, or `null` when the control is used outside a field.
 */
export function useFieldState(): FieldState | null {
  return useContext(FieldContext);
}

/** Props for {@link Field}. */
export interface FieldProps {
  /** Label text. */
  label: string;
  /** The control: an input, a select, a group of buttons. */
  children: ReactNode;
  /** Guidance shown under the control while it is valid. */
  hint?: ReactNode;
  /**
   * Error message. Its presence is what puts the field into the error state.
   *
   * Explicitly `string | undefined` because of `exactOptionalPropertyTypes`: callers read errors
   * out of a dictionary and that lookup legitimately yields `undefined`.
   */
  error?: string | undefined;
  /** Whether to mark the field as required. */
  required?: boolean;
  /** Extra classes for placement. */
  className?: string;
}

/**
 * A labelled control with room for a hint and an error.
 *
 * This is about accessibility, not looks. Wiring a label to its control and an error message to
 * the control it refers to is fiddly and easy to forget, and the result is a form that works with
 * a mouse and is unusable with a screen reader. Done here, nobody has to remember
 * `aria-describedby` again.
 *
 * @param props - See {@link FieldProps}.
 * @returns The field.
 *
 * @example
 * ```tsx
 * <Field label="Amount" error={errors.amount} required>
 *   <Input type="number" step="0.01" value={amount} onChange={onAmountChange} />
 * </Field>
 * ```
 */
export function Field({ label, children, hint, error, required, className }: FieldProps) {
  const controlId = useId();
  const messageId = `${controlId}-message`;
  const message = error ?? hint;

  return (
    <FieldContext.Provider
      value={{
        controlId,
        describedBy: message ? messageId : undefined,
        invalid: Boolean(error),
      }}
    >
      <div className={cn("flex flex-col gap-1.5", className)}>
        <label htmlFor={controlId} className="text-sm font-medium text-ink">
          {label}
          {required ? (
            <span className="ml-0.5 text-negative" aria-hidden="true">
              *
            </span>
          ) : null}
        </label>

        {children}

        {message ? (
          <p
            id={messageId}
            className={cn("text-xs", error ? "text-negative" : "text-ink-faint")}
            // Announced as it appears, rather than when the user next tabs into the field.
            role={error ? "alert" : undefined}
          >
            {message}
          </p>
        ) : null}
      </div>
    </FieldContext.Provider>
  );
}
