import { Button, Dialog, Field, Input, Select } from "@myhome/ui";
import { useEffect, useMemo, useState } from "react";
import { ApiError, type FieldErrors } from "../../api/client";
import { defaultDateForMonth } from "../../lib/format";
import {
  useAccounts,
  useIncomeCategories,
  useRegisterIncome,
  type CategorySummary,
  type IncomeRecurrence,
} from "./useIncome";

/** Props for {@link NewIncomeDialog}. */
export interface NewIncomeDialogProps {
  /** Whether the dialog is open. */
  open: boolean;
  /** Called when the dialog should close, whether the income was saved or not. */
  onClose: () => void;
  /** Month currently on screen, as `YYYY-MM-01`. The date field defaults inside it. */
  month: string;
}

/** The form's fields while they are being edited: all strings, as the DOM holds them. */
interface FormState {
  accountId: string;
  categoryId: string;
  amount: string;
  occurredOn: string;
  description: string;
  recurrence: IncomeRecurrence;
}

/** Recurrence options, in the order they are offered. */
const RECURRENCE_OPTIONS: readonly { value: IncomeRecurrence; label: string }[] = [
  { value: "Once", label: "Puntual" },
  { value: "Monthly", label: "Mensual" },
  { value: "BiMonthly", label: "Bimestral" },
  { value: "Quarterly", label: "Trimestral" },
];

/**
 * Reads an amount typed by a person into a number.
 *
 * @param value - What the user typed.
 * @returns The amount, or `null` if it is not a number.
 *
 * @remarks
 * Accepts both separators. The interface is in Spanish, where the decimal separator is a comma,
 * and `<input type="number">` handles that inconsistently across browsers. Hence a text input with
 * `inputMode="decimal"`.
 */
function parseAmount(value: string): number | null {
  const normalised = value.trim().replace(",", ".");

  if (normalised === "") {
    return null;
  }

  const amount = Number(normalised);

  return Number.isFinite(amount) ? amount : null;
}

/** A parent category with the children that can be picked under it. */
interface CategoryGroup {
  parent: CategorySummary;
  children: CategorySummary[];
}

/**
 * Arranges the flat category list into parents and children.
 *
 * @param categories - Categories as published by the API.
 * @returns One group per top-level category, children in the order received.
 */
function groupCategories(categories: readonly CategorySummary[]): CategoryGroup[] {
  const parents = categories.filter((category) => category.parentId === null);

  return parents.map((parent) => ({
    parent,
    children: categories.filter((category) => category.parentId === parent.id),
  }));
}

/**
 * The form for recording an income. Three things here are worth copying into the forms that come
 * after it:
 *
 * - Errors come from the API, not from a second set of rules written in the client. Duplicating
 *   the validation is how the two slowly stop agreeing.
 * - The idempotency key is generated once per opening, so saving twice after a timeout cannot
 *   record the income twice.
 * - Nothing is recalculated on success: the mutation invalidates the dashboard and the figures
 *   refresh themselves.
 *
 * @param props - See {@link NewIncomeDialogProps}.
 * @returns The dialog with the form.
 */
export function NewIncomeDialog({ open, onClose, month }: NewIncomeDialogProps) {
  const accounts = useAccounts();
  const categories = useIncomeCategories();
  const register = useRegisterIncome();

  const emptyForm = useMemo<FormState>(
    () => ({
      accountId: "",
      categoryId: "",
      amount: "",
      occurredOn: defaultDateForMonth(month),
      description: "",
      recurrence: "Once",
    }),
    [month],
  );

  const [form, setForm] = useState<FormState>(emptyForm);
  const [errors, setErrors] = useState<FieldErrors>({});
  const [mutationId, setMutationId] = useState(() => crypto.randomUUID());

  const groups = useMemo(() => groupCategories(categories.data ?? []), [categories.data]);

  // Each opening is a new expense: fresh fields, no stale errors, and a new idempotency key so it
  // is not mistaken for a retry of the previous one.
  useEffect(() => {
    if (open) {
      setForm(emptyForm);
      setErrors({});
      setMutationId(crypto.randomUUID());
      register.reset();
    }
    // register is a stable mutation object; listing it would re-run this on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, emptyForm]);

  const update = (field: keyof FormState) => (value: string) =>
    setForm((current) => ({ ...current, [field]: value }));

  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();

    const amount = parseAmount(form.amount);

    if (amount === null) {
      // The only check done here, and only because the field is text: the API cannot say anything
      // useful about a value that never became a number.
      setErrors({ amount: ["Escribe un importe, por ejemplo 42,35."] });
      return;
    }

    setErrors({});

    register.mutate(
      {
        accountId: form.accountId,
        categoryId: form.categoryId,
        amount,
        occurredOn: form.occurredOn,
        description: form.description.trim(),
        recurrence: form.recurrence,
        clientMutationId: mutationId,
      },
      {
        onSuccess: onClose,
        onError: (error) => {
          if (error instanceof ApiError && error.isValidation) {
            setErrors(error.fieldErrors);
          }
        },
      },
    );
  };

  const failure =
    register.error instanceof ApiError && !register.error.isValidation
      ? register.error.message
      : null;

  const isLoadingOptions = accounts.isPending || categories.isPending;

  return (
    <Dialog
      open={open}
      onClose={onClose}
      title="Añadir ingreso"
      description="Se registra en el mes de la fe cha que indiques."
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={register.isPending}>
            Cancelar
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="new-income-form"
            disabled={register.isPending || isLoadingOptions}
          >
            {register.isPending ? "Guardando…" : "Guardar ingreso"}
          </Button>
        </>
      }
    >
      <form id="new-income-form" onSubmit={handleSubmit} className="flex flex-col gap-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Importe" error={errors["amount"]?.[0]} required>
            <Input
              value={form.amount}
              onChange={(event) => update("amount")(event.target.value)}
              inputMode="decimal"
              placeholder="42,35"
              autoFocus
            />
          </Field>

          <Field label="Fecha" error={errors["occurredOn"]?.[0]} required>
            <Input
              type="date"
              value={form.occurredOn}
              onChange={(event) => update("occurredOn")(event.target.value)}
            />
          </Field>
        </div>

        <Field label="Concepto" error={errors["description"]?.[0]} required>
          <Input
            value={form.description}
            onChange={(event) => update("description")(event.target.value)}
            placeholder="Nómina, devolución de impuestos…"
            maxLength={200}
          />
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Cuenta" error={errors["accountId"]?.[0]} required>
            <Select
              value={form.accountId}
              onChange={(event) => update("accountId")(event.target.value)}
              disabled={isLoadingOptions}
            >
              <option value="">Elige una cuenta</option>
              {(accounts.data ?? []).map((account) => (
                <option key={account.id} value={account.id}>
                  {account.name}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="Categoría" error={errors["categoryId"]?.[0]} required>
            <Select
              value={form.categoryId}
              onChange={(event) => update("categoryId")(event.target.value)}
              disabled={isLoadingOptions}
            >
              <option value="">Elige una categoría</option>
              {groups.map(({ parent, children }) =>
                // A parent with children is a heading, not a choice. Recording against the leaf
                // is what lets the report aggregate by parent and stay precise.
                children.length === 0 ? (
                  <option key={parent.id} value={parent.id}>
                    {parent.name}
                  </option>
                ) : (
                  <optgroup key={parent.id} label={parent.name}>
                    {children.map((child) => (
                      <option key={child.id} value={child.id}>
                        {child.name}
                      </option>
                    ))}
                  </optgroup>
                ),
              )}
            </Select>
          </Field>
        </div>

        <Field
          label="Periodicidad"
          error={errors["recurrence"]?.[0]}
          hint={
            form.recurrence === "Once"
              ? undefined
              : "Se guarda como gasto recurrente. El apunte de este mes se registra igual."
          }
        >
          <Select
            value={form.recurrence}
            onChange={(event) => update("recurrence")(event.target.value)}
          >
            {RECURRENCE_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </Select>
        </Field>

        {failure ? (
          <p
            className="rounded-control bg-negative-bg px-3 py-2 text-sm text-negative"
            role="alert"
          >
            No se pudo guardar: {failure}
          </p>
        ) : null}
      </form>
    </Dialog>
  );
}
