import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppShell } from "./AppShell";
import { DashboardPage } from "./features/dashboard/DashboardPage";
import "./styles.css";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Financial data changes when the user changes it, not on its own. Refetching on window
      // focus only produces flicker.
      refetchOnWindowFocus: false,
      staleTime: 30_000,
    },
  },
});

const container = document.getElementById("root");
if (!container) {
  throw new Error("Missing #root element in index.html.");
}

createRoot(container).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <AppShell>
        <DashboardPage />
      </AppShell>
    </QueryClientProvider>
  </StrictMode>,
);
