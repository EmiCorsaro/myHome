import type { ReactNode } from "react";
import { cn } from "../lib/cn";

/** One column of a {@link DataTable}. */
export interface Column<TRow> {
  /** Stable identifier for the column. Used as the React key. */
  key: string;
  /** Header text. */
  header: string;
  /** Horizontal alignment. Use `end` for amounts: right-aligned figures are what make a column scannable. */
  align?: "start" | "end";
  /** Renders the cell for one row. */
  render: (row: TRow) => ReactNode;
  /**
   * Hides the column on narrow screens. For context that helps on a laptop and is noise on a
   * phone.
   */
  hideOnMobile?: boolean;
}

/** Props for {@link DataTable}. */
export interface DataTableProps<TRow> {
  /** Column definitions, in display order. */
  columns: readonly Column<TRow>[];
  /** Rows to render. */
  rows: readonly TRow[];
  /** Returns a stable key for a row. */
  getRowKey: (row: TRow) => string;
  /** What to show when there are no rows. */
  empty?: ReactNode;
  /** Extra classes for placement. */
  className?: string;
}

/**
 * The house table.
 *
 * Columns are data, not markup, so no screen invents its own padding, alignment or header style.
 * The empty state lives inside the component too, so none can forget to have one.
 *
 * It scrolls horizontally instead of squashing: a table that wraps its amounts stops being
 * readable.
 *
 * @param props - See {@link DataTableProps}.
 * @returns The table, or the empty state.
 *
 * @example
 * ```tsx
 * const columns: Column<Movement>[] = [
 *   { key: "date", header: "Date", render: (m) => formatDate(m.occurredOn) },
 *   { key: "what", header: "Description", render: (m) => m.description },
 *   { key: "amount", header: "Amount", align: "end", render: (m) => <Money value={m.amount} /> },
 * ];
 *
 * <DataTable columns={columns} rows={movements} getRowKey={(m) => m.id} />
 * ```
 */
export function DataTable<TRow>({
  columns,
  rows,
  getRowKey,
  empty,
  className,
}: DataTableProps<TRow>) {
  if (rows.length === 0) {
    return (
      <div className={cn("rounded-card border border-line bg-surface p-6", className)}>
        {empty ?? <p className="text-center text-sm text-ink-faint">Nothing to show yet.</p>}
      </div>
    );
  }

  return (
    <div className={cn("overflow-x-auto rounded-card border border-line bg-surface", className)}>
      <table className="w-full border-collapse text-sm">
        <thead>
          <tr className="border-b border-line">
            {columns.map((column) => (
              <th
                key={column.key}
                scope="col"
                className={cn(
                  "px-4 py-2.5 text-xs font-medium tracking-wide text-ink-faint uppercase",
                  column.align === "end" ? "text-right" : "text-left",
                  column.hideOnMobile && "hidden sm:table-cell",
                )}
              >
                {column.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {rows.map((row) => (
            <tr key={getRowKey(row)} className="hover:bg-surface-sunken">
              {columns.map((column) => (
                <td
                  key={column.key}
                  className={cn(
                    "px-4 py-3 align-middle text-ink",
                    column.align === "end" ? "text-right" : "text-left",
                    column.hideOnMobile && "hidden sm:table-cell",
                  )}
                >
                  {column.render(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
