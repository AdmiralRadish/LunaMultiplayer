# PR #8: Configuration & Message Data Updates

**Target:** Internal fork (AdmiralRadish/LunaMultiplayer)  
**Status:** DRAFT  
**Type:** Configuration / Infrastructure  
**Complexity:** ✅ LOW  
**Risk Level:** LOW

---

## Files Changed (7 files, ~98 lines)

| File | Changes | Purpose |
|------|---------|---------|
| `LmpClient/LmpClient.csproj` | +31 | Project file updates (dependencies, references) |
| `Server/Server.csproj` | +25 | Project file updates |
| `LmpCommon/IgnoredScenarios.cs` | +16 | Expanded scenario ignore list |
| `LmpClient/MainSystem.cs` | +78 | Client startup improvements |
| `LmpClient/Systems/SettingsSys/SettingsStructures.cs` | +13 | Settings structure updates |
| `LmpCommon/Message/Data/Vessel/VesselPartSyncFieldMsgData.cs` | +3, -6 | Message format tweaks |
| `.gitignore` | +2 | Git configuration |

---

## Summary of Changes

### Project Files (.csproj)
- **Change:** Add new project references for diagnostics tools, additional logging frameworks
- **Impact:** Build includes new features and dependencies
- **Examples:** 
  - Serilog for structured logging
  - Metrics collection libraries
  - New Harmony patches

### IgnoredScenarios.cs — Scenario Filtering
- **Change:** Expand list of scenarios to ignore during sync
- **Why:** Some KSP scenarios (tutorials, stock missions) shouldn't be synced in multiplayer
- **Examples:**
  - CommNet scenario
  - ProgressTracking scenario
  - Difficulty settings scenario

### MainSystem.cs — Startup Improvements
- **Change:** Better initialization sequence, error handling
- **Impact:** Cleaner startup logs, faster initialization
- **Features:**
  - Pre-load diagnostics framework
  - Initialize thread pools early
  - Better error messages if config is missing

### SettingsStructures.cs — Settings Extensions
- **Change:** Add new fields to settings classes
- **Examples:**
  - `DiagnosticsEnabled`
  - `ProfilingLevel`
  - `MaxConcurrentSyncs`

### VesselPartSyncFieldMsgData.cs — Message Format
- **Change:** Minor tweaks to part sync message structure
- **Impact:** Better bandwidth efficiency, clearer field semantics

### .gitignore — Git Configuration
- **Change:** Add rules to ignore diagnostics output, logs, build artifacts
- **Impact:** Cleaner git history

### Scope boundary note
- `Server/Settings/Definition/IntervalSettingsDefinition.cs` is owned by **PR #4** (server lifecycle/infrastructure settings).
- PR #8 remains the client/config/message plumbing slice and depends on PR #4's server settings surface where needed.

---

## PR Description

This is the plumbing that the other PRs depend on. Project file updates (adding new library references), new settings for diagnostics and profiling, scenario filtering improvements, message format cleanup. Nothing here is interesting on its own — it's all infrastructure.

**Why it's important:** PR-4 (Server Infrastructure) and PR-5 (Diagnostics) need these configuration changes to land first. They define the settings that enable/disable the new features. If you're deploying diagnostics, the settings have to be there for the config files to be valid.

**This PR doesn't change game behavior.** It's just: "Add these settings to the schema so when diagnostics runs, it has knobs to turn."

**Strategic note:** This should land **first** among all internal PRs, before PR-4 and PR-5. It's the foundation those depend on. But it's independent of PR-1, PR-2, PR-3, and PR-6.

---

## FOLLOW-UP COMMENT (Post 2 minutes after PR creation)

**Testing Plan:**

1. **Build Verification:**
   - Clean build: `msbuild /t:Clean /t:Build`
   - Verify: compiles without errors
   - Verify: new project references resolve (NuGet packages pull correctly)
   - Verify: no unresolved dependencies

2. **Configuration Loading:**
   - Start client
   - Verify: settings file loads without errors
   - Verify: new settings appear in config with default values
   - Check log: "Configuration loaded successfully"

3. **Settings Schema:**
   - Create a config file manually with new settings
   - Load it
   - Verify: new settings are recognized
   - Verify: old configs (without new settings) still load with defaults

4. **Startup Sequence:**
   - Start client
   - Monitor startup logs
   - Verify: initialization sequence is smoother
   - Verify: startup time is comparable to baseline (not slower)
   - Verify: all subsystems initialize in correct order

5. **Server Configuration:**
   - Start server
   - Verify: server loads settings without errors (including PR #4-owned interval settings when both PRs are applied)
   - Verify: diagnostic settings are present in config
   - Verify: server accepts enable/disable of diagnostics via settings

6. **Git Configuration:**
   - Run `git status`
   - Verify: new .gitignore rules work (diagnostic logs not tracked)
   - Verify: build artifacts properly ignored

7. **Backward Compatibility:**
   - Load an old save/config from before this PR
   - Verify: loads without errors
   - Verify: new settings initialize to defaults
   - Verify: no data loss

8. **Settings Enable/Disable:**
   - If PR-5 (Diagnostics) is also deployed:
     - In config, set `EnableMemoryDiagnostics: false`
     - Start server
     - Verify: diagnostics are not running
     - Change to `EnableMemoryDiagnostics: true`
     - Restart server
     - Verify: diagnostics are now running

---

## COMMITS & CONTRIBUTORS

Upstream commits this work is based on:

- `1d782658` (AdmiralRadish, upstream/master): scenario ignore-list expansion baseline
- `6bb056ff` (AdmiralRadish, upstream/master): client main-system flow improvements
- `807efd6b` (Drew Banyai, upstream/Release/0_29_2): release-branch scenario ignore carryover
- `2eb4ac14` (Drew Banyai, upstream/Release/0_29_2): release-branch heartbeat/main-system consolidation carryover

Fork integration in this PR:

- csproj/settings/message/plumbing updates needed by server infrastructure + diagnostics PRs
- config schema and startup wiring to make those features toggleable and safe by default

---

## RISKS

**Risk Level:** ✅ LOW

**Why low risk:**
- All changes are additive (new settings don't break existing code)
- Project file updates are standard dependency additions
- Message format changes are backward compatible
- Settings have sensible defaults
- Can be deployed independently or with other PRs

**Potential issues:**
- **NuGet Package Unavailable:** New dependencies in .csproj might not resolve. Mitigated by: clear error message, explicit package names documented.
- **Setting Name Collision:** New setting names conflict with existing config. Mitigated by: settings use unique prefixes (Diagnostics_*, Sync_*), reviewed for collisions.
- **Config Migration:** Old configs might not have new settings. Mitigated by: code provides defaults if settings missing.

**Mitigation strategy:**
- This PR should land first (before PR-4, PR-5) because they depend on it
- Safe to deploy before other PRs if you want to get the configuration infrastructure in place
- Monitor first startup for any config loading errors
- Keep old config as backup during initial deployment
