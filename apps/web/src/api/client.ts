/*
  None of the query hooks pass `signal`, deliberately. TanStack Query aborts a request when its
  last observer goes away, and StrictMode makes that happen on every mount in development: each
  query fires, is aborted, and fires again. The server then sees a client that hung up mid-query,
  and everything holding the request's cancellation token — tenant resolution, EF Core, Npgsql —
  throws, stopping the debugger on a false alarm several times per page load.

  What we give up is cancelling a request nobody is waiting for. At this size that is a few
  discarded kilobytes. The parameter stays for the day a genuinely slow endpoint earns it back.
*/


/** Validation messages keyed by the field they belong to. */
export type FieldErrors = Record<string, string[]>;

/** Error from an API call that returned an unsuccessful status. */
export class ApiError extends Error {
  /**
   * @param status - HTTP status code received.
   * @param detail - Human-readable detail, taken from the Problem Details body when present.
   * @param fieldErrors - Per-field messages, when the API returned a validation problem.
   */
  constructor(
    readonly status: number,
    detail: string,
    readonly fieldErrors: FieldErrors = {},
  ) {
    super(detail);
    this.name = "ApiError";
  }

  /** Whether the request failed because of the data sent, rather than for any other reason. */
  get isValidation(): boolean {
    return this.status === 400;
  }
}

/** The shape RFC 9457 defines for an error, including the validation variant. */
interface ProblemDetails {
  detail?: string;
  title?: string;
  errors?: FieldErrors;
}

/**
 * Turns an unsuccessful response into an {@link ApiError}, keeping the per-field messages.
 *
 * @param response - The response received.
 * @returns The error to throw.
 */
async function toError(response: Response): Promise<ApiError> {
  // The API answers with RFC 9457, so the body is usually useful. A proxy or a crash can still
  // return HTML, and failing to parse that must not hide the status code.
  const problem = (await response.json().catch(() => null)) as ProblemDetails | null;

  return new ApiError(
    response.status,
    problem?.detail ?? problem?.title ?? response.statusText,
    problem?.errors ?? {},
  );
}

/**
 * Performs a GET against the API and returns the deserialised body.
 *
 * @param path - Relative path, starting with `/api`.
 * @param signal - Abort signal. See the note below before wiring one up.
 * @returns The response body.
 * @throws {ApiError} If the response is not successful.
 *
 * @example
 * ```ts
 * const household = await apiGet<Household>("/api/household");
 * ```
 */
export async function apiGet<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(path, {
    headers: { Accept: "application/json" },
    ...(signal ? { signal } : {}),
  });

  if (!response.ok) {
    throw await toError(response);
  }

  return (await response.json()) as T;
}

/**
 * Performs a POST against the API and returns the deserialised body.
 *
 * @param path - Relative path, starting with `/api`.
 * @param body - Payload, serialised as JSON.
 * @param signal - Abort signal.
 * @returns The response body.
 * @throws {ApiError} If the response is not successful. A 400 carries `fieldErrors`, ready to be
 *   shown under each input.
 *
 * @example
 * ```ts
 * const expense = await apiPost<RegisteredExpense>("/api/expenses", {
 *   accountId,
 *   categoryId,
 *   amount: 42.35,
 *   occurredOn: "2026-08-14",
 *   description: "Weekly shop",
 * });
 * ```
 */
export async function apiPost<T>(path: string, body: unknown, signal?: AbortSignal): Promise<T> {
  const response = await fetch(path, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify(body),
    ...(signal ? { signal } : {}),
  });

  if (!response.ok) {
    throw await toError(response);
  }

  return (await response.json()) as T;
}
