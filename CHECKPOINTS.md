# CHECKPOINTS — Evaluación del estado final

> En sistemas multi-agente no se evalúa el camino, se evalúa el destino.
> Estos son los checkpoints objetivos que un juez (humano o IA) puede usar
> para decidir si el trabajo de una sesión está sano.

## C1 — El arnés está completo

- [ ] Existen los archivos de orientación: `AGENTS.md`, `CLAUDE.md`,
      `CHECKPOINTS.md`, `SETUP.md`.
- [ ] Existen los 4 subagentes en `.claude/agents/`: `leader`, `spec_author`,
      `implementer`, `reviewer`.
- [ ] Existe `specs/backlog.md` y su recuento de épicas, historias y dudas
      abiertas coincide con lo que hay en disco.

## C2 — La spec está lista antes del código

- [ ] La historia implementada tiene su `specs/<EXX-epica>/<NNN-historia>/spec.md`.
- [ ] Esa spec está en estado `refinada`, con **cero** entradas
      `[NECESITA ACLARACIÓN]` y la sección "Dudas abiertas" vacía.
- [ ] Ningún requisito implementado está ausente de la spec: no hay
      funcionalidad inventada sobre la marcha.
- [ ] `specs/backlog.md` refleja el estado real de esa historia.

## C3 — El estado es coherente

- [ ] Como mucho una historia en `in_progress` en `feature_list.json`.
- [ ] `progress/current.md` está vacío o describe la sesión activa, sin
      restos de sesiones anteriores.
- [ ] La rama de trabajo contiene los cambios de **una sola** historia.

## C4 — El código respeta la arquitectura

- [ ] El código nuevo vive donde le corresponde: dominio en
      `backend/src/Modules.*/Domain/`, casos de uso en `Application/`,
      DTOs en `Contracts/`, HTTP en `backend/src/Api/Endpoints/`,
      pantallas en `apps/web/src/features/<área>/`, componentes
      compartidos en `packages/ui/src/components/`.
- [ ] No hay EF Core en la capa HTTP ni ASP.NET en el dominio
      (`backend/tests/Architecture.Tests/` lo verifica).
- [ ] Ningún paquete NuGet nuevo se versiona fuera de
      `backend/Directory.Packages.props`.
- [ ] Los identificadores, nombres de test y comentarios están en inglés.
- [ ] No quedan logs de depuración sueltos ni TODOs sin contexto.

## C5 — La verificación es real

- [ ] `dotnet build backend/MyHome.slnx --configuration Release` termina en 0
      (con `TreatWarningsAsErrors`, esto también cubre warnings y advisories).
- [ ] `dotnet test backend/MyHome.slnx --configuration Release` ejecuta > 0
      tests y todos pasan.
- [ ] `npm run format:check`, `npm run lint`, `npm run typecheck` y
      `npm run build` terminan en 0 si se tocó frontend.
- [ ] Cada requisito funcional `RF-<n>` de la spec está cubierto por al menos
      un test concreto, o su ausencia está justificada por escrito en
      `progress/impl_<historia>.md`.

## C6 — La rodaja vertical está completa

- [ ] Si la historia tiene parte visible, hay cambios en `apps/web/` y no solo
      en el backend.
- [ ] Si la historia toca el modelo de datos, existe su migración en
      `backend/src/Modules.*/Persistence/Migrations/`.
- [ ] Los casos límite y el comportamiento ante errores descritos en la spec
      están implementados, no solo el camino feliz.

## C7 — La sesión se cerró bien

- [ ] `git status` no muestra archivos sin trackear sospechosos
      (`bin/`, `obj/`, `dist/`, `*.log`, temporales).
- [ ] `progress/history.md` tiene una entrada de la última sesión.
- [ ] El estado de la historia trabajada quedó reflejado en
      `specs/backlog.md` y en `feature_list.json`.
- [ ] Si la historia creó o modificó un endpoint HTTP, el leader dejó (o
      explícitamente saltó, avisando por qué) el ejemplo correspondiente en la
      colección `myHome API` del workspace personal de Postman (`AGENTS.md` §7).
      Este punto lo verifica el leader, no el reviewer: el reviewer no tiene
      acceso al MCP de Postman y su veredicto `APPROVED`/`CHANGES_REQUESTED` no
      depende de él.

---

**Cómo usar este archivo:** el agente revisor (`.claude/agents/reviewer.md`)
recorre cada checkbox, marca `[x]` o `[ ]` en su informe, y rechaza el cierre
si quedan casillas vacías en C1–C7.
