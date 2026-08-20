/**
 * Date formatting for the interface.
 *
 * Amounts are not here — those go through the `Money` component. Dates get a module because they
 * arrive as plain `YYYY-MM-DD` strings, and turning those into a `Date` is where a time zone
 * quietly moves a purchase to the previous day.
 */

/** Locale the interface is presented in. */
const LOCALE = "es-ES";

const SHORT_DATE = new Intl.DateTimeFormat(LOCALE, { day: "numeric", month: "short" });
const LONG_MONTH = new Intl.DateTimeFormat(LOCALE, { month: "long", year: "numeric" });

/**
 * Parses a `YYYY-MM-DD` string as a local date.
 *
 * @param isoDate - Date as published by the API.
 * @returns The equivalent local date.
 *
 * @remarks
 * `new Date("2026-08-14")` parses as UTC midnight, which in a negative-offset zone is the 13th.
 * Building it from the parts keeps the day the user actually recorded.
 */
export function parseApiDate(isoDate: string): Date {
  const [year, month, day] = isoDate.split("-").map(Number);

  return new Date(year ?? 1970, (month ?? 1) - 1, day ?? 1);
}

/**
 * Formats a date as day and abbreviated month: `14 ago`.
 *
 * @param isoDate - Date as published by the API.
 * @returns The formatted date.
 *
 * @example
 * ```ts
 * formatShortDate("2026-08-14"); // "14 ago"
 * ```
 */
export function formatShortDate(isoDate: string): string {
  return SHORT_DATE.format(parseApiDate(isoDate));
}

/**
 * Formats a date as month and year: `agosto de 2026`.
 *
 * @param isoDate - Date as published by the API.
 * @returns The formatted month.
 */
export function formatMonth(isoDate: string): string {
  return LONG_MONTH.format(parseApiDate(isoDate));
}

/**
 * Today's date as `YYYY-MM-DD`, in the browser's own zone.
 *
 * @returns The date, ready to be used as the value of a `<input type="date">`.
 *
 * @remarks
 * Built from the local parts, not `toISOString()`, which converts to UTC first and returns
 * yesterday for anyone east of Greenwich late in the evening.
 */
export function todayAsInputValue(): string {
  const now = new Date();
  const month = `${now.getMonth() + 1}`.padStart(2, "0");
  const day = `${now.getDate()}`.padStart(2, "0");

  return `${now.getFullYear()}-${month}-${day}`;
}

/**
 * First day of the month a date belongs to, as `YYYY-MM-01`.
 *
 * @param date - Any date. Defaults to today.
 * @returns The month key used to address a month across the app.
 */
export function monthKey(date: Date = new Date()): string {
  const month = `${date.getMonth() + 1}`.padStart(2, "0");

  return `${date.getFullYear()}-${month}-01`;
}

/**
 * Moves a month key forwards or backwards.
 *
 * @param key - Month key, as returned by {@link monthKey}.
 * @param months - How many months to move. Negative goes back.
 * @returns The resulting month key.
 *
 * @example
 * ```ts
 * shiftMonth("2026-08-01", -1); // "2026-07-01"
 * ```
 */
export function shiftMonth(key: string, months: number): string {
  const date = parseApiDate(key);
  date.setMonth(date.getMonth() + months);

  return monthKey(date);
}

/**
 * The date a new movement should default to when a given month is on screen.
 *
 * @param key - Month key currently being shown.
 * @returns Today if that is the month on screen, otherwise its first day.
 *
 * @remarks
 * Defaulting to today while looking at March files the expense in the wrong month, and nobody
 * notices until the totals stop matching the bank.
 */
export function defaultDateForMonth(key: string): string {
  return key === monthKey() ? todayAsInputValue() : key;
}
