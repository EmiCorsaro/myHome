# AGENTS.md — Mapa de navegación para agentes de IA

> Punto de entrada para cualquier agente que trabaje en este repositorio. No es una
> biblia de reglas: es un **mapa**. Lee solo lo que necesites cuando lo necesites
> (divulgación progresiva).

---

## 1. Qué es este repositorio

**myHome** — aplicación de finanzas de un hogar compartido. Monorepo con dos mitades:

- **Backend .NET 10**, monolito modular en `backend/`, orquestado con Aspire sobre
  PostgreSQL.
- **Frontend React 19 + TypeScript + Vite**, workspaces npm en `apps/` y `packages/`.

La prosa de documentación y specs está en **español**; el **código, los identificadores,
los nombres de test y los mensajes de commit están en inglés**.

## 2. Antes de empezar (obligatorio)

1. Lee `SETUP.md` si es la primera vez que se levanta el entorno.
2. Lee `specs/backlog.md` — es el índice de todo el trabajo especificado y su estado.
3. Lee la spec concreta de lo que vas a tocar:
   `specs/<EXX-epica>/<NNN-historia>/spec.md`.
4. Lee los documentos de `docs/` que apliquen (§3). `docs/06-scope-review.md` manda
   sobre el alcance; `docs/04-technology-stack.md` sobre las decisiones técnicas.
5. Verifica que el árbol está verde antes de tocar nada (§6).

## 3. Mapa del repositorio

### Especificaciones y planificación

| Ruta | Qué contiene | Cuándo leerlo |
|---|---|---|
| `specs/backlog.md` | Índice de épicas e historias: prioridad, estado (`borrador` / `refinada`) y dudas abiertas | Siempre, al empezar |
| `specs/<EXX-epica>/epic.md` | Contexto, valor y lista de historias de una épica | Antes de tocar cualquier historia de esa épica |
| `specs/<EXX-epica>/<NNN-historia>/spec.md` | El contrato de una historia: requisitos en EARS, casos límite, fuera de alcance | Antes de implementar |
| `specs/<EXX-epica>/<NNN-historia>/jira-fields.md` | Campos listos para cargar el work item a mano en Jira | Solo al exportar |
| `feature_list.json` | Cola local de implementación: una entrada por historia, derivada de `backlog.md` | Al coordinar la implementación |
| `init.sh` | Verificación del entorno, del estado del repositorio y del árbol | Al empezar y antes de cerrar |

La numeración de historias es **global y correlativa** (001, 002, 003…): no se reinicia
por épica, para poder referenciar una historia sin nombrar su épica. `specs/` está en
`.gitignore`: vive solo en local.

### Documentación de producto y arquitectura (`docs/`, también local)

| Archivo | Qué contiene |
|---|---|
| `docs/01-vision-and-scope.md` | El problema y para quién |
| `docs/02-domain-model.md` | Modelo de dominio completo (referencia de lo diferido) |
| `docs/03-use-cases.md` | Casos de uso en Gherkin |
| `docs/04-technology-stack.md` | Stack, requisitos no funcionales y decisiones |
| `docs/05-roadmap.md` | Orden de construcción por fases |
| `docs/06-scope-review.md` | **Manda sobre el alcance.** Qué entra en fase 1 y qué se difiere |
| `docs/07-frontend-architecture-and-style.md` | Arquitectura de frontend y sistema visual |
| `docs/08-backlog-presupuesto.md` | Backlog de presupuestado vs real |

Cuando `02` y `06` discrepen, **gana `06`**.

### Código backend (`backend/`)

| Ruta | Qué contiene |
|---|---|
| `backend/MyHome.slnx` | Solución canónica, la que compila CI. `Home.slnx` es un duplicado heredado |
| `backend/src/AppHost/` | Orquestador Aspire: levanta Postgres, migra, siembra, arranca API y Vite |
| `backend/src/Api/` | Capa HTTP: `Endpoints/`, `ErrorHandling/`, `Hosting/`, `Tenancy/` |
| `backend/src/Modules.Ledger/` | Módulo de contabilidad: cuentas, categorías, asientos, planificados, reglas recurrentes |
| `backend/src/Modules.Shared/` | Núcleo compartido: `Household`, `Money`, entidades base, multi-tenancy |
| `backend/Directory.Packages.props` | Versiones centralizadas de paquetes NuGet |
| `backend/Directory.Build.props` | Propiedades comunes (incluye `TreatWarningsAsErrors`) |

Cada módulo sigue la misma estructura interna:

```
Modules.<Nombre>/
  Domain/                     entidades y objetos de valor, sin dependencias de framework
  Application/Interfaces/<X>/ contratos de caso de uso
  Application/Services/       implementaciones y validadores
  Contracts/<Área>/           DTOs de entrada y salida
  Persistence/                DbContext, Configurations/, Migrations/
```

### Código frontend (`apps/`, `packages/`)

| Ruta | Qué contiene |
|---|---|
| `apps/web/src/features/<área>/` | Una carpeta por área funcional: página + hooks de datos |
| `apps/web/src/api/client.ts` | Cliente HTTP contra la API |
| `apps/web/src/AppShell.tsx` | Layout y navegación |
| `packages/ui/src/components/` | Componentes compartidos |
| `packages/ui/src/tokens.css` | Tokens de diseño |

### Tests

| Ruta | Qué cubre |
|---|---|
| `backend/tests/Shared.Tests/` | Dominio compartido (`Money`, entidades base) |
| `backend/tests/Ledger.Tests/` | Dominio y servicios del ledger |
| `backend/tests/Architecture.Tests/` | Fronteras de módulo: nada de EF Core en la capa HTTP, nada de ASP.NET en el dominio |

**Todavía no hay tests de frontend** ni runner configurado. Si una historia necesita
cobertura de UI, decídelo y déjalo escrito en la spec antes de escribir el primer test.

### Arnés de agentes

| Ruta | Qué contiene |
|---|---|
| `CLAUDE.md` | Rol obligatorio de la sesión principal (leader) |
| `CHECKPOINTS.md` | Criterios objetivos de "estado final correcto" |
| `.claude/agents/` | Subagentes: `leader`, `spec_author`, `implementer`, `reviewer` |
| `.claude/skills/` | Skills de spec: `spec-generator`, `refinar-spec`, `generar-salida-spec` |
| `progress/` | Bitácora de la sesión actual y reportes de subagentes |

`.claude/` está en `.gitignore`.

## 4. Flujo de trabajo

### Fase de spec (fuera de código)

```
idea → [spec-generator] → borrador con [NECESITA ACLARACIÓN]
     → [refinar-spec] → refinada, sin dudas abiertas
     → [generar-salida-spec] → jira-fields.md → work item en Jira
```

Una spec nace en **borrador**, con cada hueco visible como
`[NECESITA ACLARACIÓN: ...]` y recogido en "Dudas abiertas". **Nunca se rellena un hueco
inventando.** Solo una spec sin dudas abiertas pasa a `refinada` y puede exportarse.

Las specs **no llevan referencias de dependencia** entre épicas o historias: esas
relaciones se crean a mano en Jira.

### Fase de implementación

```
spec refinada → in_progress → [implementer → reviewer] → done
```

Una historia es una **rodaja vertical completa**: interfaz de usuario, backend, modelo de
datos y tests. No se implementa partida por capa técnica.

## 5. Reglas duras (no negociables)

- **Una sola historia a la vez.** No mezcles cambios de varias historias en la misma sesión.
- **No implementes sin spec refinada.** Si la spec tiene dudas abiertas, para y refínala.
- **No inventes requisitos.** Si la spec no lo dice, no se construye: se pregunta.
- **No declares nada terminado sin el árbol verde** (§6).
- **Respeta las fronteras de módulo.** `Architecture.Tests` las verifica y CI las rompe.
- **Código en inglés, documentación y specs en español.**
- **Documenta lo que haces** en `progress/current.md` mientras trabajas, no al final.
- **Si no sabes algo, búscalo en la spec o en `docs/`** antes de inventarlo.

## 6. Verificación

Atajo, desde la raíz del repositorio:

```bash
./init.sh          # entorno + estado del repositorio + build + tests
./init.sh --quick  # solo entorno y estado, sin compilar
```

`init.sh` encadena exactamente lo que viene a continuación, más la validación de
`feature_list.json` contra las specs en disco. Los pasos sueltos, por si necesitas
uno solo:

Backend, desde la raíz del repositorio:

```bash
dotnet build backend/MyHome.slnx --configuration Release
dotnet test  backend/MyHome.slnx --configuration Release
```

`TreatWarningsAsErrors` está activo: un warning, o un paquete con advisory conocida,
rompe la compilación.

Frontend, desde la raíz (nunca desde dentro de `apps/web`):

```bash
npm run format:check
npm run lint
npm run typecheck
npm run build
```

Levantar la aplicación completa:

```bash
dotnet run --project backend/src/AppHost
```

Estos son exactamente los pasos de `.github/workflows/ci.yml`. Si pasan en local, pasan
en CI.

## 7. Ejemplos en Postman

Cada endpoint HTTP nuevo o modificado se acompaña de un ejemplo real en Postman,
para poder probarlo sin escribirlo a mano. Esto lo hace **el leader**, nunca el
implementer ni el reviewer (ninguno de los dos tiene el MCP de Postman en su lista
de herramientas): es el último paso antes de dejar la historia lista para que la
humana la marque `done`, y solo después de que el reviewer haya emitido `APPROVED`.

- **Siempre en el workspace personal.** Nunca en un workspace de equipo, aunque
  exista uno.
- **Una sola colección para todo el backend**: `myHome API`, con una carpeta por
  área (Household, Ledger, Categories, Expenses, Dashboard, ...), reflejando el
  agrupamiento de `backend/src/Api/Endpoints/`.
- **Un request por endpoint**, nombrado `MÉTODO /ruta — descripción corta` (p. ej.
  `POST /api/categories — Crear categoría`), con URL, headers y body reales
  extraídos del contrato (`Contracts/`) y del propio endpoint, no placeholders
  genéricos.
- **Un ejemplo guardado (request + response)** colgando de cada request, con una
  respuesta realista (payload válido de verdad, código de estado correcto).
- Si el endpoint ya tenía un request en la colección, se **actualiza**, no se
  duplica.
- Si el MCP de Postman no está conectado en la sesión, se salta este paso y se le
  avisa explícitamente a la humana en vez de darlo por hecho.

## 8. Cierre de sesión

1. El árbol está verde (§6).
2. El estado de la historia queda reflejado donde corresponda (`specs/backlog.md`,
   `feature_list.json`, Jira).
3. El resumen de `progress/current.md` se mueve al final de `progress/history.md` y
   `progress/current.md` queda con la plantilla vacía.
4. No quedan archivos temporales, ni logs de depuración, ni TODOs sin contexto.

## 9. Si te bloqueas

- Relee la spec y el documento de `docs/` que aplique.
- Si la herramienta no hace lo que esperas, **no inventes un workaround**: documenta el
  bloqueo en `progress/current.md` y para la sesión.
