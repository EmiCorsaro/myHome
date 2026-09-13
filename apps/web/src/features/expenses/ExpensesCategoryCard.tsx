import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
} from "../../components/ui/card";
import { Progress } from "../../components/ui/progress";
import { BarChart3 } from "lucide-react";
/*import {
  useAccounts,
  useExpenseCategories,
  type CategorySummary,
  type ExpenseRecurrence,
} from "./useExpenses";*/

interface ExpenseCategory {
  category: string;
  amount: number;
}

interface ExpensesCategoryCardProps {
  expenses: ExpenseCategory[];
  totalBudget: number;
}

export function ExpensesCategoryCard({ expenses, totalBudget }: ExpensesCategoryCardProps) {
  // Función para formatear a euros
  const formatCurrency = (value: number) => {
    return new Intl.NumberFormat("es-ES", {
      style: "currency",
      currency: "EUR",
    }).format(value);
  };

  return (
    <Card className="w-full max-w-xl border-slate-200 shadow-sm">
      <CardHeader className="flex flex-row items-start justify-between pb-6">
        <div className="space-y-1">
          <CardTitle className="text-xl font-bold text-slate-900">Gastos por categoría</CardTitle>
          <CardDescription className="text-slate-500 text-base">
            Distribución de los gastos registrados este mes.
          </CardDescription>
        </div>
        <BarChart3 className="text-emerald-500 w-6 h-6" strokeWidth={2} />
      </CardHeader>

      <CardContent className="space-y-5">
        {expenses.map((item) => {
          // 2. Calculamos el porcentaje basado en el presupuesto total.
          // Usamos Math.min para asegurar que la barra no pase del 100%
          // en caso de que los gastos superen el presupuesto.
          const rawPercentage = totalBudget > 0 ? (item.amount / totalBudget) * 100 : 0;
          const safePercentage = Math.min(rawPercentage, 100);

          return (
            <div key={item.category} className="space-y-2">
              <div className="flex justify-between items-center text-sm">
                <span className="font-semibold text-slate-700">{item.category}</span>
                <span className="font-bold text-slate-900">{formatCurrency(item.amount)}</span>
              </div>
              <Progress
                value={safePercentage}
                className="h-2.5 bg-slate-100 --color-barra-porcentual"
              />
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}
