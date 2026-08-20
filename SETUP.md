# Setup

Everything needed to go from a fresh clone to a running application. Should take about twenty
minutes, most of it downloads.

## 1. Install four things

| Tool                                                              | Why                               |
| ----------------------------------------------------------------- | --------------------------------- |
| [Git](https://git-scm.com/downloads)                              | Source control                    |
| [Node 24 LTS](https://nodejs.org/)                                | Frontend and tooling              |
| [.NET 10 SDK](https://dotnet.microsoft.com/download)              | Backend                           |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | PostgreSQL, started automatically |

Nothing else. No global npm packages, no database installed on the machine, no IDE plugins that
have to be configured by hand.

Then pick an editor. Both are set up already:

- **Visual Studio** — open `backend/MyHome.slnx`. Set `AppHost` as the startup project.
- **VS Code** — open the repository root. It will offer to install the recommended extensions;
  accept.

## 2. Trust the development certificate

```bash
dotnet dev-certs https --trust
```

**Run this before anything else.** Aspire's orchestrator talks to itself over HTTPS and refuses
to start without a _trusted_ certificate. When it is missing, the failure is several hundred
lines of Kubernetes watch-task stack traces that never mention the word "certificate". A Windows
trust prompt will appear; accept it.

## 3. Install dependencies

```bash
npm install
```

From the repository root, not from inside `apps/web`. This is an npm workspaces monorepo: a
single install at the root wires up `apps/web`, `packages/ui` and `packages/api-client`.

## 4. Run it

```bash
dotnet run --project backend/src/AppHost
```

That one command starts PostgreSQL in a container, creates the schema, seeds a working
household, starts the API, and starts Vite. The Aspire dashboard opens with the logs, traces and
metrics of all three, and the web app is linked from it.

In Visual Studio the equivalent is F5 with `AppHost` as the startup project. In VS Code it is
the default build task (`Ctrl+Shift+B`).

The first run pulls the PostgreSQL image, so it takes a couple of minutes. Later runs are quick,
and the data survives restarts.

### Ports

Fixed on purpose, in `AppHost.cs`. Stable URLs mean bookmarks keep working and both developers
see the same addresses — and, more importantly, they avoid the Windows reserved-range problem
described at the bottom of this page.

| Service    | Port | What it is                                |
| ---------- | ---- | ----------------------------------------- |
| Web app    | 5173 | The application                           |
| PostgreSQL | 5432 | Database, for an external client          |
| pgweb      | 8081 | Browse the database in a browser          |
| API        | 5216 | From `Api/Properties/launchSettings.json` |

The Aspire dashboard picks its own port and prints it on startup.

## Changing the starting accounts and categories

Both live in `backend/src/Modules.Ledger/Persistence/LedgerSeeder.cs`, in `StarterAccounts`,
`ExpenseCategories` and `IncomeCategories`. They are the only user-facing strings in the backend,
so they are written in Spanish.

They are only created when the household has no accounts yet. To pick up an edit, drop the
database volume and let the next run rebuild it:

```bash
docker volume rm myhome-pgdata
```

## Working on the frontend without the backend

For building components, a backend is unnecessary overhead:

```bash
npm run dev --workspace @myhome/web
```

This reads `apps/web/.env.development` to find the API. If the API is not running, screens that
fetch data will show their error state — which is a good reason to design that state properly.

## Before pushing

CI runs formatting, linting, types, both builds, all tests and a vulnerability scan. To check
locally in one go, run the `check: everything` task in VS Code, or:

```bash
npm run lint && npm run typecheck && dotnet test backend/MyHome.slnx
```

Formatting is checked, not just suggested: a badly formatted file fails the build. With format
on save enabled — it is, in the committed settings — this never comes up.

## When something does not work

| Symptom                                                                                              | Cause                                                                      |
| ---------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| Hundreds of lines of Kubernetes stack traces                                                         | The development certificate is not trusted. Go back to step 2.             |
| `Cannot connect to the Docker daemon`                                                                | Docker Desktop is not running.                                             |
| `VITE_API_URL is not set`                                                                            | The frontend was started outside Aspire and `.env.development` is missing. |
| Port already in use                                                                                  | A previous run is still alive. Close it, or restart Docker Desktop.        |
| The web app loads but every request returns 401                                                      | The API is up but the database is empty. Restart the AppHost so it seeds.  |
| `Unable to allocate a network port`, then `Service postgres should have valid address at this point` | Windows has the ports reserved. See below.                                 |

### Windows reserves ports, and Aspire does not say so clearly

Windows sets aside large TCP ranges for Hyper-V and WSL2. If those blocks cover the band Aspire
allocates dynamic ports from, every resource needing one fails at the same time and startup dies
complaining that PostgreSQL has no address — which points nowhere near the cause.

To see the reserved ranges:

```bash
netsh interface ipv4 show excludedportrange protocol=tcp
```

This is why every port in `AppHost.cs` is fixed and sits low, far below the ephemeral range. If a
new resource is added and fails this way, give it an explicit port rather than debugging the
allocator.
