import { useEffect, useRef, type ReactNode } from "react";
import { cn } from "../lib/cn";

/** Props for {@link Dialog}. */
export interface DialogProps {
  /** Whether the dialog is open. */
  open: boolean;
  /** Called when the user closes it: the close button, `Escape`, or clicking the backdrop. */
  onClose: () => void;
  /** Title, announced when the dialog opens. */
  title: string;
  /** Supporting line under the title. */
  description?: string;
  /** Dialog content. */
  children: ReactNode;
  /** Actions, aligned to the right at the bottom. */
  footer?: ReactNode;
  /** Extra classes for the panel. */
  className?: string;
}

/**
 * A modal dialog, built on the native `<dialog>`.
 *
 * That gets four things right for free that a hand-rolled modal usually gets wrong: focus moves in
 * on open and back on close, `Escape` closes it, the page behind goes inert, and it renders on top
 * without a `z-index` argument.
 *
 * @param props - See {@link DialogProps}.
 * @returns The dialog.
 *
 * @example
 * ```tsx
 * <Dialog
 *   open={isOpen}
 *   onClose={close}
 *   title="New expense"
 *   footer={<Button variant="primary" form="expense-form" type="submit">Save</Button>}
 * >
 *   <ExpenseForm id="expense-form" onSaved={close} />
 * </Dialog>
 * ```
 */
export function Dialog({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  className,
}: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const element = ref.current;

    if (!element) {
      return;
    }

    // showModal() and not the open attribute: only the former gives the focus trap and the inert
    // background. open="true" renders the same box with none of the behaviour.
    if (open && !element.open) {
      element.showModal();
    } else if (!open && element.open) {
      element.close();
    }
  }, [open]);

  return (
    <dialog
      ref={ref}
      // Escape and close() both land here, so React state stays the single source of truth for
      // whether the dialog is open.
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClose={onClose}
      // A click on the dialog element itself, rather than the panel inside it, is the backdrop.
      onClick={(event) => {
        if (event.target === ref.current) {
          onClose();
        }
      }}
      aria-labelledby="dialog-title"
      className={cn(
        // No explicit width: the browser's dialog styling keeps it inside the viewport on a
        // phone, max-w-lg caps it on a laptop.
        "m-auto w-full max-w-lg rounded-card border border-line bg-surface p-0",
        "text-ink backdrop:bg-ink/30",
        className,
      )}
    >
      <div className="flex flex-col gap-4 p-5">
        <header className="flex flex-col gap-1">
          <h2 id="dialog-title" className="text-lg font-medium text-ink">
            {title}
          </h2>
          {description ? <p className="text-sm text-ink-soft">{description}</p> : null}
        </header>

        {children}

        {footer ? <footer className="flex justify-end gap-2 pt-1">{footer}</footer> : null}
      </div>
    </dialog>
  );
}
