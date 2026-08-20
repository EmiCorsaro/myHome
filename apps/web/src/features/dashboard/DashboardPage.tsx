import {
  Button,
  Card,
  CategoryBreakdown,
  DataTable,
  EmptyState,
  Money,
  Section,
  StatCard,
  categoryTone,
  cn,
  type Column,
} from "@myhome/ui";
import { useState } from "react";
import { NewExpenseDialog } from "../expenses/NewExpenseDialog";
import { formatMonth, formatShortDate, monthKey, shiftMonth } from "../../lib/format";
import { useDashboard, type AccountSummary, type LedgerEntrySummary } from "./useDashboard";

/**
 * The landing screen: what came in, what went out, where it went, what is left.
 *
 * Every figure comes from the API and is rendered without a single addition here. Once the
 * frontend starts summing there are two implementations of the household's arithmetic, and no way
 * to tell which is right when they disagree.
 *
 * @returns The dashboard.
 */
export function DashboardPage() {
  const [month, setMonth] = useState(monthKey);
  const [isAddingExpense, setIsAddingExpense] = useState(false);

  const { data, isPending, error } = useDashboard(month);
  const isCurrentMonth = month === monthKey();

  if (isPending) {
    return <p className="text-sm text-ink-soft">Cargando el panel…</p>;
  }

  if (error) {
    return (
      <div className="rounded-card border border-negative/30 bg-negative-bg p-4">
        <p className="text-sm font-medium text-negative">No se pudo cargar el panel</p>
        <p className="mt-1 text-sm text-ink-soft">{error.message}</p>
      </div>
    );
  }

  const hasExpenses = data.byCategory.length > 0;

  return (
    <div className="flex flex-col gap-8">
      <Section
        title={capitalise(formatMonth(data.periodStart))}
        description={isCurrentMonth ? "Mes en curso" : "Estás viendo otro mes"}
        actions={
          <>
            <div className="flex items-center gap-1">
              <Button
                variant="ghost"
                aria-label="Mes anterior"
                onClick={() => setMonth(shiftMonth(month, -1))}
              >
                ‹
              </Button>
              <Button
                variant="ghost"
                aria-label="Mes siguiente"
                onClick={() => setMonth(shiftMonth(month, 1))}
              >
                ›
              </Button>
              {isCurrentMonth ? null : (
                <Button variant="ghost" onClick={() => setMonth(monthKey())}>
                  Hoy
                </Button>
              )}
            </div>

            {/* Next sub-phase. Disabled rather than hidden so the layout does not shift later. */}
            <Button disabled title="Disponible en la próxima etapa">
              Añadir ingreso
            </Button>
            <Button variant="primary" onClick={() => setIsAddingExpense(true)}>
              Añadir gasto
            </Button>
          </>
        }
      >
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <StatCard
            label="Ingresos"
            value={data.income}
            currency={data.currency}
            hint="Cobrado en el mes"
          />
          <StatCard
            label="Gastos"
            value={data.expense}
            currency={data.currency}
            hint="Registrado en el mes"
          />
          <StatCard
            label="Diferencia"
            value={data.net}
            currency={data.currency}
            colorize
            hint="Ingresos menos gastos"
          />
          <StatCard
            label="Saldo hoy"
            value={data.trackedBalance}
            currency={data.currency}
            // Not a figure for the month on screen: this is the balance right now.
            hint="Cuentas incluidas en la proyección"
          />
        </div>
      </Section>

      <div className="grid gap-6 lg:grid-cols-2">
        <Section title="Gastos por categoría" description="Dónde se fue el dinero este mes">
          <Card>
            {hasExpenses ? (
              <CategoryBreakdown
                items={data.byCategory.map((category) => ({
                  id: category.categoryId,
                  name: category.name,
                  colorIndex: category.colorIndex,
                  total: category.total,
                  share: category.share,
                }))}
                currency={data.currency}
              />
            ) : (
              <EmptyState
                title="Aún no hay gastos en este mes"
                description="En cuanto registres el primero, aparecerá aquí el reparto por categoría."
                action={
                  <Button variant="primary" onClick={() => setIsAddingExpense(true)}>
                    Añadir gasto
                  </Button>
                }
              />
            )}
          </Card>
        </Section>

        <Section title="Proyección de ahorro" description="Hacia dónde va el saldo">
          <Card className="h-full">
            <ProjectionPlaceholder
              isAvailable={data.isProjectionAvailable}
              net={data.net}
              currency={data.currency}
            />
          </Card>
        </Section>
      </div>

      <Section title="Cuentas" description="Saldo actual de cada una">
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
          {data.accounts.map((account) => (
            <AccountCard key={account.id} account={account} />
          ))}
        </div>
      </Section>

      <Section title="Movimientos del mes" description="Lo registrado en el mes que estás viendo">
        <DataTable
          columns={MOVEMENT_COLUMNS}
          rows={data.recentEntries}
          getRowKey={(entry) => entry.id}
          empty={
            <EmptyState
              title="Todavía no hay movimientos"
              description="Registra un gasto y lo verás aquí al instante."
            />
          }
        />
      </Section>

      <NewExpenseDialog
        open={isAddingExpense}
        onClose={() => setIsAddingExpense(false)}
        month={month}
      />
    </div>
  );
}

/** Columns of the movements table. Outside the component so they are built once. */
const MOVEMENT_COLUMNS: readonly Column<LedgerEntrySummary>[] = [
  {
    key: "date",
    header: "Fecha",
    render: (entry) => <span className="text-ink-soft">{formatShortDate(entry.occurredOn)}</span>,
  },
  {
    key: "description",
    header: "Concepto",
    render: (entry) => (
      <span className="flex items-center gap-2">
        <span className="font-medium">{entry.description}</span>
        {entry.isRecurring ? (
          <span className="rounded-control bg-surface-sunken px-1.5 py-0.5 text-xs text-ink-faint">
            recurrente
          </span>
        ) : null}
      </span>
    ),
  },
  {
    key: "category",
    header: "Categoría",
    render: (entry) =>
      entry.categoryName ? (
        <span className="flex items-center gap-2">
          <span
            className={cn(
              "size-2.5 shrink-0 rounded-full",
              categoryTone(entry.categoryColorIndex ?? 10).background,
            )}
            aria-hidden="true"
          />
          <span className="text-ink-soft">{entry.categoryName}</span>
        </span>
      ) : (
        <span className="text-ink-faint">—</span>
      ),
  },
  {
    key: "account",
    header: "Cuenta",
    hideOnMobile: true,
    render: (entry) => <span className="text-ink-soft">{entry.accountName}</span>,
  },
  {
    key: "amount",
    header: "Importe",
    align: "end",
    render: (entry) => <Money value={entry.amount} />,
  },
];

/**
 * One account with its balance, and a warning when it is under its floor.
 *
 * @param props - The account to show.
 * @param props.account - Account data as published by the API.
 * @returns The card.
 */
function AccountCard({ account }: { account: AccountSummary }) {
  const isBelowBuffer =
    account.minimumBufferTarget !== null && account.balance < account.minimumBufferTarget;

  return (
    <Card className="flex flex-col gap-1">
      <span className="truncate text-sm font-medium text-ink-soft">{account.name}</span>
      <Money
        value={account.balance}
        currency={account.currency}
        colorize={false}
        className="text-xl font-semibold"
      />
      {isBelowBuffer ? (
        // The one colour on this screen that is a warning and not decoration.
        <span className="text-xs font-medium text-caution">Por debajo del colchón previsto</span>
      ) : (
        <span className="text-xs text-ink-faint">
          {account.isTracked ? "En la proyección" : "Fuera de la proyección"}
        </span>
      )}
    </Card>
  );
}

/**
 * The projection card while there is nothing to project from.
 *
 * @param props - What is known so far.
 * @param props.isAvailable - Whether the API can compute a projection yet.
 * @param props.net - Income minus expense for the month on screen.
 * @param props.currency - Base currency.
 * @returns The card's content.
 *
 * @remarks
 * Says what is missing instead of extrapolating one month into a curve. A forecast built from a
 * handful of manual entries looks like an answer without being one, and this screen is only worth
 * anything if it can be believed when it says the money will not stretch.
 */
function ProjectionPlaceholder({
  isAvailable,
  net,
  currency,
}: {
  isAvailable: boolean;
  net: number;
  currency: string;
}) {
  if (isAvailable) {
    return null;
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-col gap-1">
        <span className="text-sm text-ink-soft">Ritmo de este mes</span>
        <Money value={net} currency={currency} className="text-2xl font-semibold" />
        <p className="text-xs text-ink-faint">
          Diferencia entre lo cobrado y lo gastado hasta ahora.
        </p>
      </div>

      <p className="rounded-control bg-surface-sunken px-3 py-2 text-sm text-ink-soft">
        La proyección de saldo llega cuando estén cargados los ingresos y gastos recurrentes. Con
        una sola cifra de un mes no se puede anticipar el valle del saldo, que es lo que importa
        saber.
      </p>
    </div>
  );
}

/**
 * Capitalises the first letter.
 *
 * @param value - Text to capitalise.
 * @returns The text with its first letter in upper case.
 *
 * @remarks
 * `Intl.DateTimeFormat` gives Spanish month names in lower case: right in a sentence, wrong as a
 * heading.
 */
function capitalise(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1);
}
