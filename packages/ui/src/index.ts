/**
 * Public surface of `@myhome/ui`.
 *
 * Everything a screen may use comes from here. Paths inside the package are not a contract: they
 * can move without notice.
 */

export { Button, type ButtonProps, type ButtonVariant } from "./components/Button";
export { Card, type CardProps } from "./components/Card";
export {
  CategoryBreakdown,
  type CategoryBreakdownItem,
  type CategoryBreakdownProps,
} from "./components/CategoryBreakdown";
export { DataTable, type Column, type DataTableProps } from "./components/DataTable";
export { Dialog, type DialogProps } from "./components/Dialog";
export { EmptyState, type EmptyStateProps } from "./components/EmptyState";
export { Field, useFieldState, type FieldProps } from "./components/Field";
export { Input, Select, type InputProps, type SelectProps } from "./components/Input";
export { Money, type MoneyProps, type MoneySignDisplay } from "./components/Money";
export { Section, type SectionProps } from "./components/Section";
export { StatCard, type StatCardProps } from "./components/StatCard";
export { categoryTone, type CategoryTone } from "./lib/categoryTone";
export { cn } from "./lib/cn";
