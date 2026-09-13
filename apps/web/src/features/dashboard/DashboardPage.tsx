import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "../../components/ui/card";
/*import { Progress } from "@/components/ui/progress"*/
import { Button } from "../../components/ui/button";
import {
  Building2,
  TrendingUp,
  TrendingDown,
  PiggyBank,
  ChevronLeft,
  ChevronRight,
  BarChart3,
  DollarSign,
} from "lucide-react";
import { useState, useEffect } from "react";
import { NewExpenseDialog } from "../expenses/NewExpenseDialog";
import { NewIncomesDialog } from "../incomes/NewIncomesDialog";
import { formatMonth, monthKey, shiftMonth } from "../../lib/format";
import { useDashboard, useGetExpenses, useGetIncomes } from "./useDashboard";

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
  const [balance, setBalance] = useState(0);
  const [isAddingExpense, setIsAddingExpense] = useState(false);
  const [isAddingIncomes, setIsAddingIncomes] = useState(false);

  const { data, isPending, error } = useDashboard(month);
  const { data: expensesData } = useGetExpenses();
  const { data: incomesData } = useGetIncomes();

  useEffect(() => {
    fetch("/api/dashboard")
      .then((response) => response.json())
      .then((data) => {
        console.log("Datos del panel:", data);
        if (data.accounts && data.accounts.length > 0) {
          const balanceValue = data.accounts[0].balance;
          setBalance(balanceValue);
        }
      })
      .catch((error) => console.error("Error al traer datos:", error));
  }, []);

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

  /*const hasExpenses = data.byCategory.length > 0;*/

  return (
    <div className="min-h-screen bg-slate-50/50 p-6 md:p-10 text-slate-800">
      {/* 1. TOP BAR / NAVBAR SUPERIOR */}
      <div className="max-w-7xl mx-auto flex-row md:flex-row justify-between items-start md:items-center pb-6 border-b border-slate-200 mb-8 gap-4">
        <div className="flex flex-row gap-1 justify-between">
          <div className="flex flex-row items-center gap-2 text-indigo-900 font-bold text-2xl">
            <span className="p-1.5 bg-emerald-600 rounded-lg text-white">
              <Building2 className="h-5 w-5" />
            </span>
            MyHome
          </div>
          <div className="flex flex-row items-center gap-2 text-slate-500 font-medium text-sm">
            {/*Next sub-phase. Disabled rather than hidden so the layout does not shift later.*/}
            <Button variant="default" onClick={() => setIsAddingIncomes(true)}>
              Añadir ingreso
            </Button>
            <Button variant="default" onClick={() => setIsAddingExpense(true)}>
              Añadir gasto
            </Button>
          </div>
        </div>
      </div>
      <div className="max-w-7xl mx-auto space-y-8">
        {/* 2. SUB-NAV / PESTAÑAS Y NAVEGACIÓN POR MES */}
        <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
          <div className="flex items-center gap-6 border-b border-slate-200 w-full sm:w-auto">
            <button className="text-emerald-700 font-semibold border-b-2 border-emerald-700 pb-2 text-sm px-1">
              <a href="/DashboardPage">Resumen</a>
            </button>
            <button className="text-slate-500 font-medium hover:text-slate-800 pb-2 text-sm px-1">
              <a href="/Transactions">Transacciones</a>
            </button>
            <button className="text-slate-500 font-medium hover:text-slate-800 pb-2 text-sm px-1">
              <a href="/Budgets">Presupuestos</a>
            </button>
          </div>

          {/* Selector de Fecha */}
          <div
            className="flex items-center gap-1 bg-white border border-slate-200 rounded-lg px-2 py-1 shadow-sm"
            title={capitalise(formatMonth(data.periodStart))}
          >
            <Button
              variant="ghost"
              size="icon"
              aria-label="Mes anterior"
              className="h-8 w-8 text-slate-500"
              onClick={() => setMonth(shiftMonth(month, -1))}
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <span> {capitalise(formatMonth(data.periodStart))} </span>
            <Button
              variant="ghost"
              size="icon"
              aria-label="Mes siguiente"
              className="h-8 w-8 text-slate-500"
              onClick={() => setMonth(shiftMonth(month, 1))}
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>

        {/* 3. TÍTULO DE LA SECCIÓN */}
        <div>
          <h2 className="text-xl font-bold text-slate-900">Panorama mensual</h2>
          <p className="text-slate-500 text-sm">
            Tus movimientos del mes, reunidos en una sola vista.
          </p>
        </div>

        {/* 4. GRID DE TARJETAS DE MÉTRICAS */}
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {/* Saldo Disponible */}
          <Card className="shadow-sm border-slate-100 bg-white">
            <CardHeader className="flex flex-row items-center justify-between pb-2 space-y-0">
              <CardTitle className="text-sm font-medium text-slate-500">Saldo disponible</CardTitle>
              <Building2 className="h-7 w-7 text-emerald-600 bg-emerald-50 rounded p-0.5" />
            </CardHeader>
            <CardContent>
              <div className="text-center text-2xl font-bold text-slate-900">{balance} €</div>
            </CardContent>
          </Card>

          {/* Ingresos */}
          <Card className="shadow-sm border-slate-100 bg-white">
            <CardHeader className="flex flex-row items-center justify-between pb-2 space-y-0">
              <CardTitle className="text-sm font-medium text-slate-500">Ingresos del mes</CardTitle>
              <TrendingUp className="h-7 w-7 text-emerald-600" />
            </CardHeader>
            <CardContent>
              <div className="text-center text-2xl font-bold text-emerald-600">
                {(Array.isArray(incomesData)
                  ? incomesData
                  : incomesData
                    ? [incomesData]
                    : []
                ).reduce((total, income) => total + income.amount, 0)}{" "}
                €
              </div>
            </CardContent>
          </Card>

          {/* Gastos */}
          <Card className="shadow-sm border-slate-100 bg-white">
            <CardHeader className="flex flex-row items-center justify-between pb-2 space-y-0">
              <CardTitle className="text-sm font-medium text-slate-500">Gastos del mes</CardTitle>
              <TrendingDown className="h-7 w-7 text-rose-600" />
            </CardHeader>
            <CardContent>
              <div className="text-center text-2xl font-bold text-rose-600">
                {(Array.isArray(expensesData)
                  ? expensesData
                  : expensesData
                    ? [expensesData]
                    : []
                ).reduce((total, expense) => total + expense.amount * -1, 0)}{" "}
                €
              </div>
            </CardContent>
          </Card>

          {/* Ahorro */}
          <Card className="shadow-sm border-slate-100 bg-white">
            <CardHeader className="flex flex-row items-center justify-between pb-2 space-y-0">
              <CardTitle className="text-sm font-medium text-slate-500">Ahorro del mes</CardTitle>
              <PiggyBank className="h-7 w-7 text-indigo-900" />
            </CardHeader>
            <CardContent>
              <div className="text-center text-2xl font-bold text-indigo-900">0 €</div>
            </CardContent>
          </Card>
        </div>

        {/* 5. SECCIÓN INFERIOR DE DOS COLUMNAS */}
        <div className="grid gap-6 md:grid-cols-3">
          {/* Gastos por categoría */}
          <Card className="md:col-span-2 shadow-sm border-slate-100 bg-white">
            <CardHeader>
              <div className="flex justify-between items-center">
                <div>
                  <CardTitle className="text-base font-bold text-slate-900">
                    Gastos por categoría
                  </CardTitle>
                  <CardDescription className="text-slate-500 text-xs">
                    Distribución de los gastos registrados este mes.
                  </CardDescription>
                </div>
                <BarChart3 className="h-6 w-6 text-emerald-600" />
              </div>
            </CardHeader>
            <CardContent className="space-y-5">
              {/*{datosCategorias.map((item) => (
                <div key={item.nombre} className="space-y-1.5">
                  <div className="flex justify-between text-sm font-medium text-slate-700">
                    <span>{item.nombre}</span>
                    <span>{item.monto.toFixed(2).replace('.', ',')} €</span>
                  </div>
                    Barra de progreso de shadcn/ui estilizada en verde esmeralda
                  <Progress value={item.porcentaje} className="h-2 bg-slate-100 [&>div]:bg-emerald-700" />
                </div>
              ))}*/}
            </CardContent>
          </Card>

          {/* Resumen de Ahorro */}
          <Card className="shadow-sm border-slate-100 bg-emerald-50/40 flex flex-col justify-between">
            <CardHeader>
              <div className="flex justify-between items-center">
                <CardTitle className="text-base font-bold text-slate-900">
                  Resumen de ahorro
                </CardTitle>
                <DollarSign className="h-4 w-4 text-emerald-600 border border-emerald-200 rounded-full bg-white p-0.5" />
              </div>
              <CardDescription className="text-slate-500 text-xs pt-1">
                El ahorro representa la diferencia entre los ingresos y los gastos de este mes.
              </CardDescription>
            </CardHeader>

            <CardContent className="space-y-6 pt-4">
              <div className="bg-white border border-emerald-100 p-4 rounded-xl shadow-xs">
                <span className="text-xs text-slate-500 font-medium block mb-1">
                  Tasa de ahorro mensual
                </span>
                <span className="text-3xl font-extrabold text-slate-800">11%</span>
              </div>

              <Button
                variant="link"
                className="text-emerald-700 font-semibold p-0 flex items-center gap-1 hover:no-underline"
              >
                Ver presupuestos <ChevronRight className="h-4 w-4" />
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>
      <NewExpenseDialog
        open={isAddingExpense}
        onClose={() => setIsAddingExpense(false)}
        month={month}
      />
      <NewIncomesDialog
        open={isAddingIncomes}
        onClose={() => setIsAddingIncomes(false)}
        month={month}
      />
    </div>
  );
}

{
  /* Columns of the movements table. Outside the component so they are built once.
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
];*/
}

{
  /*
   * One account with its balance, and a warning when it is under its floor.
   *
   * @param props - The account to show.
   * @param props.account - Account data as published by the API.
   * @returns The card.
   *
   * @remarks
   * This card is used to display information about a single account and its current balance.
   */
}

{
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
