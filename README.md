# myHome — Joint Finance Management

> **Working name.** Still a placeholder. It lives in one MSBuild property
> (`ProductPrefix` in `backend/Directory.Build.props`) and one npm scope (`@myhome`),
> so changing it again stays cheap.

A household finance tool designed from day one for **two people who share an economy without
fully merging it**: income landing in different pockets, expenses split by different rules
depending on the concept, and goals that are sometimes joint and sometimes individual.

## Status

| Phase                        | Status                                            |
| ---------------------------- | ------------------------------------------------- |
| Vision and scope             | ✅ Drafted                                        |
| Domain model                 | ✅ v0.2, reduced to 11 tables for v1              |
| Use cases                    | ✅ v0.1                                           |
| Technology stack             | ✅ Decided (open to revision)                     |
| Roadmap                      | ✅ Phase 1 defined in five sub-phases             |
| Design system / visual style | ✅ Direction chosen, palette pending              |
| Phase 1.0 — Foundations      | ✅ Solution, CI, tokens, first endpoint           |
| Phase 1.1 — Ledger           | ✅ Dashboard and expense registration, end to end |
| Phase 1.2 — Recurrences      | ⏳ Next: projection needs them                    |

## Getting started

Full instructions in **[SETUP.md](SETUP.md)**. The short version, after installing Git, Node 24,
the .NET 10 SDK and Docker Desktop:

```bash
dotnet dev-certs https --trust
```

```bash
npm install
```

```bash
dotnet run --project backend/src/AppHost
```

That last command starts PostgreSQL, the API and the web app together, with the Aspire
dashboard on top of all three.

## Working in two

|        | Backend                              | Frontend                  |
| ------ | ------------------------------------ | ------------------------- |
| Owns   | `backend/`, `packages/api-client`    | `apps/web`, `packages/ui` |
| Editor | Visual Studio, `backend/MyHome.slnx` | VS Code, repository root  |

The frontend is deliberately **not** part of the solution file. Visual Studio would need an
`.esproj` to host it, which is a file only Visual Studio understands and noise for everyone
else. Aspire covers what that would have bought: one command starts everything, from either
editor.

**The API contract is the handshake.** `packages/api-client` is generated from the API's OpenAPI
document, never written by hand. A backend contract change therefore breaks the frontend's
compile, not its runtime — the person on the other side finds out on `git pull`, with an error
pointing at a line, instead of through an `undefined` three days later.

Short branches off `main`, pull request, green CI, squash merge. One rule worth keeping: **`main`
always runs.** Clone, `npm install`, F5, no questions asked.

## Architecture in one sentence

A **modular monolith** with three modules: a core ledger (`Ledger`) and two satellite modules
that depend on it but never on each other (`Goals & Wealth` and `Travel`), communicating
through in-process domain events.

```
                ┌─────────────────────┐      ┌─────────────────────┐
                │  Goals & Wealth     │      │  Travel Planner     │
                │  goals · net worth  │      │  budget · itinerary │
                └──────────┬──────────┘      └──────────┬──────────┘
                           │   events / queries         │
                ┌──────────┴────────────────────────────┴──────────┐
                │             CORE — Ledger                        │
                │  accounts · entries · recurrences · envelopes ·   │
                │  debts · cards · splits · allocation cascade      │
                └──────────────────────────────────────────────────┘
```

## Documentation

Documentation is split by what may be published.

| Location                                          | Contents                                                                                                                                                  | In the repository?   |
| ------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------- |
| `context/`                                        | The real household the model is validated against: account topology, income figures, assets, collaboration context. Written in Spanish.                   | **No** — git-ignored |
| `design/`, currently `docs/`                      | Vision and scope, domain model, use cases, technology stack, roadmap. Written in English and in the abstract, but derived from the private context above. | **No** — git-ignored |
| This README                                       | Overview, architecture summary, conventions.                                                                                                              | Yes                  |
| Usage guides, testing and test-case documentation | To be written alongside the code. English.                                                                                                                | Yes                  |

Design documents are kept local deliberately. They exist and are maintained; they are simply
not part of what gets published, because they were written against real personal data and the
risk of a residual figure or name surviving a review is not worth taking.

## The four decisions that define the product

1. **Double-entry in the domain, single-entry in the UI.** The user records "I spent €42 at
   the supermarket"; the system writes two postings that sum to zero. Without this, credit
   cards, debts with principal+interest, and savings goals all become special cases full of
   patches.
2. **Accrual and cash are two views of the same fact.** A card purchase on 3 March is a
   _March expense_ but an _April cash outflow_. Most household apps collapse the two, which
   is why their cash forecasts are fiction.
3. **Two household models must coexist.** _Reimbursement_ (each pays their own, settle at
   month end) and _common pot_ (all income lands in a central account and cascades to
   destinations). The reference household runs mostly on the second one, which is why the
   **allocation cascade**, not the settlement, is the central engine.
4. **Who pays ≠ who bears the cost.** Expense shares are modelled independently of which
   account paid. That separation is what makes fairness auditable.

## Conventions

- **The backend owns every business rule.** Clients render state and collect intent; they
  never compute what a number means. The API contract is the only boundary, so a mobile client
  can be added later without touching the domain.
- **Endpoints are thin**: bind, delegate to a service, map the response, handle errors. All
  logic lives in application services, reachable from a scheduled job as easily as from HTTP.
- **Modular monolith, kept separable.** Modules talk through contracts and events, never
  through each other's internals or database tables. Enforced by architecture tests in CI.
- **OWASP controls are built in during development**, not audited at the end.
- All published documentation, code, identifiers, commit messages, file names and folder
  names are written in **English**.
- The product UI is localised separately; the codebase is not.
- The model is validated against a real household whose data lives outside version control.
  Public documents state the conclusions in the abstract and contain no real figures.
