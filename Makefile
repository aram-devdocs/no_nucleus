# Nucleus — dev task runner.
#   make            list targets
#   make dev        build everything + deploy to the sandbox + launch the game
#   make commander  rebuild+deploy just one mod + launch (fast iteration)
# Per-mod targets only rebuild that mod's DLL; the host (platform) ships the shared libs, so after
# changing a shared lib run `make dev` (or `make platform`) to redeploy them.

SLN     := Nucleus.sln
CONFIG  := Release
APPS    := apps
LOG     := .sandbox/game/BepInEx/LogOutput.log
PS      := powershell -NoProfile -ExecutionPolicy Bypass -File
LAUNCH  := $(PS) scripts/run.ps1

.DEFAULT_GOAL := help
.PHONY: help dev run build rebuild clean test audit check logaudit smoke \
        platform commander build-mod squad warfare \
        sandbox mission codegen export-missions

help:
	@echo "Nucleus dev targets:"
	@echo "  make dev         build all mods + deploy + launch the game"
	@echo "  make run         alias for 'make dev'"
	@echo "  make build       build the whole solution (Release, 0 warnings)"
	@echo "  make rebuild     clean + build"
	@echo "  make test        full gate (8 layers: build/unit/arch/sim/logaudit/installer/contract/integration)"
	@echo "  make check       fast gate (build + core unit + arch)"
	@echo "  make logaudit    audit the last in-game BepInEx log"
	@echo "  make smoke       launch the game, verify mods load + no exceptions, kill it (self-test)"
	@echo ""
	@echo "  per-mod (rebuild+deploy that mod, then launch):"
	@echo "  make platform | make commander | make build-mod | make squad | make warfare"
	@echo ""
	@echo "  setup / content:"
	@echo "  make sandbox     create/refresh the gitignored .sandbox game mirror (+ BepInEx)"
	@echo "  make mission     install the demo mission into your user Missions folder"
	@echo "  make export-missions  dump the game's built-in missions to artifacts/ (to fork, e.g. Escalation)"
	@echo "  make dynamic-mission  fork Escalation into the Nucleus Dynamic Warfare mission (run export-missions first)"
	@echo "  make codegen     regenerate the typed game SDK (run after a game update)"
	@echo "  make clean       remove build outputs"

# ---- full build / run -------------------------------------------------------
dev: build
	@echo "[dev] launching (all mods deployed)..."
	@$(LAUNCH)

run: dev

build:
	dotnet build $(SLN) -c $(CONFIG) -p:TreatWarningsAsErrors=true

rebuild: clean build

clean:
	dotnet clean $(SLN) -c $(CONFIG)

# ---- gates ------------------------------------------------------------------
test audit:
	$(PS) scripts/audit.ps1

check:
	bash scripts/check.sh

logaudit:
	$(PS) scripts/audit.ps1 -LogPath $(LOG)

# Automated in-game smoke test: launch the game, wait for the mod's self-test markers, kill it, verdict.
smoke:
	$(PS) scripts/smoketest.ps1

# ---- per-mod fast iteration (build that mod's DLL -> deploy -> launch) -------
platform:
	dotnet build $(APPS)/Nucleus.Platform/Nucleus.Platform.csproj -c $(CONFIG)
	@$(LAUNCH)

commander:
	dotnet build $(APPS)/Nucleus.Commander/Nucleus.Commander.csproj -c $(CONFIG)
	@$(LAUNCH)

build-mod:
	dotnet build $(APPS)/Nucleus.Build/Nucleus.Build.csproj -c $(CONFIG)
	@$(LAUNCH)

squad:
	dotnet build $(APPS)/Nucleus.Squad/Nucleus.Squad.csproj -c $(CONFIG)
	@$(LAUNCH)

warfare:
	dotnet build $(APPS)/Nucleus.Warfare/Nucleus.Warfare.csproj -c $(CONFIG)
	@$(LAUNCH)

# ---- setup / content --------------------------------------------------------
sandbox:
	$(PS) scripts/setup-sandbox.ps1

mission:
	$(PS) scripts/install-demo-mission.ps1

# Dump the game's built-in missions (Escalation, etc.) to artifacts/ so they can be forked into Nucleus missions.
export-missions:
	$(PS) scripts/export-missions.ps1

# Build the Nucleus Dynamic Warfare mission by forking the game's Escalation export (run export-missions first).
dynamic-mission:
	$(PS) scripts/build-dynamic-warfare-mission.ps1

codegen:
	bash scripts/generate-types.sh
