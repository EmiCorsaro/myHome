import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, loadEnv } from "vite";

export default defineConfig(({ mode, command }) => {
  // Resolution order, highest priority first:
  //   1. process.env  — what Aspire injects when started with the AppHost. It carries the port
  //                     Aspire actually assigned to the API, so it cannot drift.
  //   2. .env.<mode>  — the fallback for running "npm run dev" on its own.
  //
  // There is deliberately no hardcoded port here. A literal would silently go stale the day
  // someone edits launchSettings.json, and the symptom would be a 404 with no explanation.
  const fileEnv = loadEnv(mode, process.cwd(), "VITE_");
  const apiUrl = process.env["VITE_API_URL"] ?? fileEnv["VITE_API_URL"];

  // Only the dev server needs it, for the proxy below. A production build serves the app and the
  // API from the same origin, so requiring it there would break CI for no reason.
  if (command === "serve" && !apiUrl) {
    throw new Error(
      "VITE_API_URL is not set. Start the app through the Aspire AppHost, or set it in " +
        "apps/web/.env.development.",
    );
  }

  return {
    plugins: [react(), tailwindcss()],
    server: {
      // Aspire assigns the port through PORT; standalone runs fall back to Vite's default.
      port: Number(process.env["PORT"] ?? 5173),
      // The browser always talks to its own origin and Vite forwards. That way the client never
      // knows the API's URL, and the same code works in production, where both are served
      // together.
      proxy: {
        "/api": { target: apiUrl ?? "", changeOrigin: true },
      },
    },
    optimizeDeps: {
      // @myhome/ui is consumed as TypeScript source from the workspace, not as a built package.
      // Excluding it from pre-bundling makes a component change show up immediately.
      exclude: ["@myhome/ui"],
    },
  };
});
