#!/usr/bin/env bash
# init.sh — Verificación del entorno y del estado del repositorio
#
# Lo ejecuta el agente al COMENZAR una sesión y antes de dar por terminada
# cualquier historia. Si falla, la sesión no debe avanzar.
#
# Uso:
#   ./init.sh            verificación completa (entorno + estado + build + tests)
#   ./init.sh --quick    solo entorno y estado del repositorio, sin compilar
#
# Los pasos de compilación y test son los mismos que ejecuta
# .github/workflows/ci.yml: si esto pasa en local, pasa en CI.

set -u

QUICK=0
[ "${1:-}" = "--quick" ] && QUICK=1

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
NC='\033[0m'

ok()   { printf "${GREEN}[OK]${NC}    %s\n" "$1"; }
warn() { printf "${YELLOW}[WARN]${NC}  %s\n" "$1"; }
fail() { printf "${RED}[FAIL]${NC}  %s\n" "$1"; }

EXIT_CODE=0
SLN="backend/MyHome.slnx"

echo "── 1. Herramientas ─────────────────────────────────────"

if ! command -v dotnet >/dev/null 2>&1; then
  fail "dotnet no está instalado (se requiere el SDK de .NET 10). Ver SETUP.md"
  exit 1
fi
DOTNET_VERSION=$(dotnet --version)
case "$DOTNET_VERSION" in
  10.*) ok "dotnet -> $DOTNET_VERSION" ;;
  *)    warn "dotnet -> $DOTNET_VERSION (el proyecto apunta a .NET 10)" ;;
esac

if ! command -v node >/dev/null 2>&1; then
  fail "node no está instalado (se requiere Node >= 22, recomendado 24 LTS). Ver SETUP.md"
  exit 1
fi
NODE_MAJOR=$(node -p "process.versions.node.split('.')[0]")
if [ "$NODE_MAJOR" -lt 22 ]; then
  fail "node $(node --version): se requiere Node >= 22"
  exit 1
fi
ok "node -> $(node --version)"

command -v npm >/dev/null 2>&1 || { fail "npm no está disponible"; exit 1; }
ok "npm -> $(npm --version)"

if [ ! -d node_modules ]; then
  warn "node_modules/ no existe: ejecuta 'npm install' desde la raíz"
fi

echo ""
echo "── 2. Archivos base del arnés ──────────────────────────"

for f in AGENTS.md CLAUDE.md CHECKPOINTS.md SETUP.md feature_list.json \
         progress/current.md package.json "$SLN"; do
  if [ ! -f "$f" ]; then
    fail "Falta archivo base: $f"
    EXIT_CODE=1
  else
    ok "Existe $f"
  fi
done

# docs/ y specs/ están en .gitignore: existen en local, no en un clon limpio.
[ -d docs ]  || warn "No existe docs/ (documentación local, excluida del remoto)"
[ -d specs ] || warn "No existe specs/ (specs locales, excluidas del remoto)"

echo ""
echo "── 3. feature_list.json y specs ────────────────────────"

node - <<'JS'
const fs = require("node:fs");

let data;
try {
  data = JSON.parse(fs.readFileSync("feature_list.json", "utf8"));
} catch (e) {
  console.log(`[FAIL]  feature_list.json ilegible: ${e.message}`);
  process.exit(1);
}

const validStatus = new Set(data.rules.valid_status);
const validSpecState = new Set(data.rules.valid_spec_state);
const errors = [];
const hasSpecs = fs.existsSync("specs");

const inProgress = data.features.filter((f) => f.status === "in_progress");
if (inProgress.length > 1) {
  errors.push(
    `Hay ${inProgress.length} historias en in_progress (máximo 1): ` +
      inProgress.map((f) => f.story).join(", "),
  );
}

const seen = new Set();
for (const f of data.features) {
  if (seen.has(f.story)) errors.push(`Historia duplicada: ${f.story}`);
  seen.add(f.story);

  if (!validStatus.has(f.status))
    errors.push(`Historia ${f.story}: estado inválido '${f.status}'`);
  if (!validSpecState.has(f.spec_state))
    errors.push(`Historia ${f.story}: estado de spec inválido '${f.spec_state}'`);

  if (hasSpecs && !fs.existsSync(f.spec))
    errors.push(`Historia ${f.story}: no existe su spec en ${f.spec}`);

  const started = ["in_progress", "in_review", "done"].includes(f.status);
  if (started && (f.spec_state !== "refinada" || f.open_questions > 0))
    errors.push(
      `Historia ${f.story} en ${f.status} con spec en '${f.spec_state}' ` +
        `y ${f.open_questions} dudas abiertas: no debería haberse implementado`,
    );
}

if (errors.length) {
  for (const e of errors) console.log(`[FAIL]  ${e}`);
  process.exit(1);
}

const refined = data.features.filter((f) => f.spec_state === "refinada").length;
console.log(`[OK]    feature_list.json válido (${data.features.length} historias)`);
console.log(`[OK]    ${refined} refinadas, ${data.features.length - refined} en borrador`);
console.log(
  inProgress.length
    ? `[OK]    En curso: ${inProgress[0].story} — ${inProgress[0].title}`
    : "[OK]    Ninguna historia en curso",
);
JS

[ $? -ne 0 ] && EXIT_CODE=1

if [ $QUICK -eq 1 ]; then
  echo ""
  echo "── Resumen (modo --quick) ──────────────────────────────"
  if [ $EXIT_CODE -eq 0 ]; then
    ok "Entorno y estado correctos. Falta compilar y pasar los tests."
  else
    fail "Estado NO válido. Resuelve los errores antes de avanzar."
  fi
  exit $EXIT_CODE
fi

echo ""
echo "── 4. Backend: build y tests ───────────────────────────"

# TreatWarningsAsErrors está activo: un warning, o un paquete con advisory
# conocida (NU1903), rompe la compilación. El análisis de dependencias no es
# un paso aparte, es la compilación.
if dotnet build "$SLN" --configuration Release; then
  ok "Backend compila"
else
  fail "El backend no compila"
  EXIT_CODE=1
fi

# Incluye Architecture.Tests: fronteras de módulo, nada de EF Core en la capa
# HTTP, nada de ASP.NET en el dominio.
if dotnet test "$SLN" --no-build --configuration Release; then
  ok "Tests del backend en verde"
else
  fail "Hay tests del backend rotos"
  EXIT_CODE=1
fi

echo ""
echo "── 5. Frontend: formato, lint, tipos y build ───────────"

if [ ! -d node_modules ]; then
  warn "Saltado: no hay node_modules/. Ejecuta 'npm install' desde la raíz"
else
  for step in format:check lint typecheck build; do
    if npm run "$step" --silent; then
      ok "npm run $step"
    else
      fail "npm run $step"
      EXIT_CODE=1
    fi
  done
fi

echo ""
echo "── 6. Resumen ──────────────────────────────────────────"

if [ $EXIT_CODE -eq 0 ]; then
  ok "Árbol verde. Puedes empezar a trabajar."
else
  fail "El árbol NO está verde. Resuelve los errores antes de avanzar."
fi

exit $EXIT_CODE
