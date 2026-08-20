import type { ReactNode } from "react";
import { useHousehold } from "./features/household/useHousehold";

/** Props for {@link AppShell}. */
export interface AppShellProps {
  /** The screen being shown. */
  children: ReactNode;
}

/**
 * The frame every screen sits in: header, width, page padding.
 *
 * Thin on purpose. It holds the maximum width and the header and nothing else. When there are
 * several screens the navigation goes here, and all of them get it at once.
 *
 * @param props - See {@link AppShellProps}.
 * @returns The frame with the screen inside it.
 */
export function AppShell({ children }: AppShellProps) {
  const { data: household } = useHousehold();

  return (
    <div className="min-h-screen bg-canvas">
      <header className="border-b border-line bg-surface">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3">
          <div className="flex min-w-0 items-baseline gap-2">
            <span className="text-sm font-semibold text-ink">myHome</span>
            {household ? (
              <span className="truncate text-sm text-ink-faint">· {household.name}</span>
            ) : null}
          </div>

          {household ? (
            <span className="shrink-0 text-xs text-ink-faint">
              {household.members.map((member) => member.displayName).join(" · ")}
            </span>
          ) : null}
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-6 sm:py-8">{children}</main>
    </div>
  );
}
