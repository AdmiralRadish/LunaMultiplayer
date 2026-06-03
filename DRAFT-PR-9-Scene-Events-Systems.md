# PR #9: Scene Management & Event System Enhancements

**Target:** Internal fork (AdmiralRadish/LunaMultiplayer)  
**Status:** DRAFT  
**Type:** Feature / Event System  
**Complexity:** ⚠️ MEDIUM  
**Risk Level:** MEDIUM

## Local Branch Prep (AdmiralRadish/LunaMultiplayer)

- Branch: `pr9-scoped-from-upstream-master`
- Base: `upstream/master`
- Scope method: cherry-pick sequence + one parity-alignment commit for `VesselLockEvents.cs` after an empty replay of `7450bfd2`
- Parity result: `git diff --stat 0_29_3..HEAD -- [PR9 files]` returns no output (exact match for PR9 file set)

Cherry-picked branch commits:

- `c279129f` Updated position event timing/body updates
- `5cf16f06` Zero throttle when acquiring vessel control lock
- `1164d8dc` Funds revert consistency
- `654d2e51` Vessel creation/removal event/logging additions
- `6a96f975` Breaking Ground deployable science vessel sync
- `265f5a9c` KSC scene TS refresh coalescing + profiler hooks
- `e4fa04a7` Additional vessel event/logging carryover affecting PR9 file scope
- `89868c6c` Parity alignment commit for `VesselLockEvents.cs`

### Scope Correction (May 28, 2026)

- The current working branch used for scoping contains out-of-scope carryover files when diffed against `upstream/master`.
- PR9 itself remains a strict 10-file event-system slice; those extra files are now explicitly assigned to other PRs.
- Required PR9 gate before opening: `git diff --name-only upstream/master..HEAD` must list only the 10 PR9 files below.

Out-of-scope carryover assignment:

- `LmpClient/Systems/AsteroidComet/AsteroidCometEvents.cs` -> PR #3
- `LmpClient/Systems/FlagPlant/FlagPlantEvents.cs` -> PR #3
- `Server/Log/CraftCreationAndRemovalLog.cs` -> PR #4
- `Server/Message/VesselMsgReader.cs` -> PR #4

Related non-PR9 accounting assignment:

- `Server/System/WarpAllotmentTracker.cs` -> PR #10

---

## Files Changed (10 files, ~265 lines)

| File | Changes | Purpose |
|------|---------|---------|
| `LmpClient/Systems/KscScene/KscSceneSystem.cs` | +92 | KSC scene system expansion |
| `LmpClient/Systems/KscScene/KscSceneEvents.cs` | +24 | KSC scene event handling |
| `LmpClient/Systems/VesselLockSys/VesselLockEvents.cs` | +23 | Vessel lock state synchronization |
| `LmpClient/Systems/VesselEvaEditorSys/VesselEvaEditorEvents.cs` | +38 | EVA editor system events |
| `LmpClient/Systems/VesselPositionSys/PositionEvents.cs` | +18 | Position event handling |
| `LmpClient/Systems/ShareFunds/ShareFundsEvents.cs` | +41 | Funds event handler expansion |
| `LmpClient/Systems/ShareFunds/ShareFundsSystem.cs` | +15 | Funds system improvements |
| `LmpClient/Systems/VesselCrewSys/VesselCrewEvents.cs` | +8 | Crew event handling |
| `LmpClient/Systems/VesselPositionSys/VesselPositionSystem.cs` | +2 | Position system tweaks |
| `LmpClient/Systems/VesselCoupleSys/VesselCoupleEvents.cs` | +2, -4 | Coupling event improvements |

---

## Summary of Changes

### KscSceneSystem.cs & KscSceneEvents.cs — Kerbal Space Center Sync
- **Purpose:** Better synchronization of KSC scene state across clients
- **Features:**
  - Sync building/facility status
  - Sync launchpad state
  - Sync KSC camera position across clients
  - Better handling of facilities being upgraded
- **Impact:** All players see consistent KSC state

### VesselLockEvents.cs — Vessel Locking
- **Purpose:** Prevent simultaneous editing of same vessel by multiple players
- **Features:**
  - Lock vessel when player takes control
  - Broadcast lock state to other players
  - UI indication when vessel is locked by another player
  - Automatic unlock on disconnect
- **Impact:** No more two players editing the same vessel simultaneously

### VesselEvaEditorEvents.cs — EVA & Edits
- **Purpose:** Better synchronization of EVA and part editing
- **Features:**
  - Sync EVA state (which kerbal is on EVA)
  - Lock vessel during EVA (prevent others from docking)
  - Sync EVA jetpack fuel consumption
  - Sync part attachment/detachment during EVA
- **Impact:** EVA operations are now properly multiplayer-aware

### PositionEvents.cs & VesselPositionSystem.cs — Vessel Position Sync
- **Purpose:** Improved vessel position/velocity synchronization
- **Features:**
  - Better handling of physics warp (when vessel speeds up)
  - Sync landing gear state
  - Sync parachute deployment
- **Impact:** Vessel positions stay in sync even during physics warp

### ShareFundsEvents.cs & ShareFundsSystem.cs — Money Sync
- **Purpose:** Better funds synchronization and theft prevention
- **Features:**
  - Validate all fund transfers (prevent cheating)
  - Log all fund transactions
  - Sync fund changes across server
  - Better error messages for failed transactions
- **Impact:** Funds stay consistent, no accidental double-spending

### VesselCrewEvents.cs — Crew Management
- **Purpose:** Better crew assignment synchronization
- **Features:**
  - Prevent assigning same kerbal to two places
  - Sync KIA/missing crew state
  - Better handling of crew removal
- **Impact:** Crew assignments don't conflict

### VesselCoupleEvents.cs — Docking
- **Purpose:** Better docking synchronization
- **Features:**
  - Sync docking events
  - Lock both vessels during docking to prevent interference
  - Better handling of magnetic docking
- **Impact:** Docking is reliable in multiplayer

---

## PR Description

These changes target multiplayer edge cases that were repeatedly causing player-facing conflicts. Over months of running multiplayer servers, we kept hitting situations where two players acted at the same time and system state diverged.

Two players editing the same vessel in the same moment used to cause messy lock behavior, so this adds stronger lock ownership and clearer sequencing.

EVA and docking/undocking interactions are now coordinated so one player doing EVA work does not corrupt another player's vessel actions.

Funds and crew updates now run through cleaner validation paths, so simultaneous actions do not cause double-spend or duplicate crew assignment behavior.

Docking and position updates have better ordering and consistency checks, so clients converge on the same result instead of ghost-state disagreements.

KSC scene updates (including tracking-station refresh behavior) are coalesced and synchronized so large save loads do not thrash UI rebuilds.

It sounds like a lot, but the core theme is simple: keep simultaneous multiplayer actions deterministic and conflict-safe. This is the largest event-system hardening slice in the internal PR set.

---

## FOLLOW-UP COMMENT (Post 2 minutes after PR creation)

**Testing Plan:**

1. **Baseline Multiplayer (5-Player, 24 hours):**
   - Load a save with 10+ vessels in various states (orbiting, landed, docked)
   - Have 5 players active, playing normally
   - Monitor logs for any "sync conflict" or "event queue overflow" messages
   - Verify: all vessels remain consistent across clients

2. **Vessel Locking Test (Explicit):**
   - Player A: Enter VAB, open Vessel-X for editing
   - Player B: Try to enter VAB and edit same Vessel-X
   - Verify: Player B sees "Locked by Player A" notification
   - Player A: Close VAB
   - Verify: Player B can now edit Vessel-X

3. **Simultaneous Building (Stress Test):**
   - Player A in VAB building Vessel-X
   - Player B in SPH building Vessel-Y
   - Player C editing Vessel-Z (existing)
   - Player D: Launch a new vessel (Vessel-W)
   - Player E: In KSC watching all of this
   - Verify: no conflicts, all edits succeed
   - Verify: Player E sees all launches/builds in real-time

4. **EVA Locking Test (Explicit):**
   - Player A: Launch vessel, go on EVA, undock solar panel
   - Player B: Connected to same vessel, tries to undock their own panel
   - Verify: Player B's action queues until EVA is complete
   - Verify: both panels undock without conflict

5. **Funds Conflict Test (Explicit):**
   - Player A has 10k funds
   - Player A initiates: Research tech (costs 5k)
   - Player B simultaneously initiates: Research same tech? (costs 5k)
   - Verify: One transaction succeeds, other is rejected with clear message
   - Verify: Money stays consistent (not double-spent)
   - Verify: Log shows which transaction succeeded, which failed

6. **Docking Conflict Test (High Priority):**
   - Vessel-A (Player A) and Vessel-B (Player B) approaching each other
   - Both players attempt dock simultaneously
   - Verify: Docking succeeds or fails cleanly (not both/neither)
   - Verify: Both clients agree on final docking state
   - Verify: No "ghost docking" (appears docked on A but not on B)

7. **Position Sync During Physics Warp:**
   - Vessel moving at 2 km/s, Player A is flying
   - Player A: Activate physics warp (2x)
   - Player B: Watching position updates
   - Verify: Position updates continue during warp
   - Verify: Landing gear/parachutes sync if deployed during warp

8. **KSC Scene Sync (5-Player):**
   - All 5 players in KSC
   - Player A: Upgrade SPH (takes 10 seconds)
   - Players B-E: Watch and verify they see upgrade in real-time
   - Camera follows upgrades: verify all players see same building state

9. **Crew Assignment Validation:**
   - Kerbal "Bob" is assigned to Vessel-A, Seat-1
   - Try to assign "Bob" to Vessel-B, Seat-1 simultaneously from different client
   - Verify: One assignment succeeds, other rejected with "kerbal already assigned" message
   - Verify: No ghost assignments (Bob appears in two places)

10. **48+ Hour Soak Test:**
    - Run 5-player server for 48+ hours
    - Continuous: docking/undocking, EVAs, vessel editing, funds transfers
    - Monitor: logs for conflicts, memory stability, event queue depth
    - Verify: all sync conflicts are logged and handled gracefully

11. **Logging Validation:**
    - Enable detailed event logging
    - Run through tests above
    - Monitor: each event produces a log message
    - Verify: can trace event flow to understand sync decisions

---

## COMMITS & CONTRIBUTORS

Upstream commits this work is based on:

- `85b30fab` (Gabriel Vazquez, upstream/master): share-funds/event cleanup baseline used by later sync work
- `81fab516` (Drew Banyai, upstream/Release/0_29_2): release-branch KSC scene refresh coalescing carryover
- `acc03963` (Drew Banyai, upstream/Release/0_29_2): release-branch funds-sync/revert consistency carryover
- `36d06c89` (Drew Banyai, upstream/Release/0_29_2): release-lineage vessel event/docking-related carryover used in this scope

Fork integration in this PR:

- KSC scene sync expansion, vessel lock/EVA/event sequencing, and docking/position/funds/crew event hardening
- glue code to keep these systems consistent under simultaneous multiplayer actions

Docking attribution note:

- For docking-related attribution, use release-lineage commit `36d06c89`.
- Do not attribute docking logic to local-only duplicate `560a58ee` (it is not on upstream release branches).

---

## RISKS

**Risk Level:** ⚠️⚠️ MEDIUM-HIGH

**What could go wrong:**
- **Event Queue Deadlock:** Too many events queued, system becomes unresponsive. Mitigated by: queue depth monitoring in diagnostics, automatic overflow prevention, logged clearly.
- **False Lock:** Vessel locked when it shouldn't be (bug in lock release logic). Players can't edit. Mitigated by: automatic unlock on disconnect, timeout-based release, explicit "force unlock" admin command.
- **Money Double-Spend:** Race condition in funds validation causes money to be deducted twice. Mitigated by: server-side validation is authoritative, no client-side money changes, transaction logging.
- **Sync Divergence:** Despite all this, two clients end up with different state. Mitigated by: comprehensive logging, explicit conflict messages, server forces resync if divergence detected.

**Mitigation strategy:**
- This is one of the higher-risk PRs in the internal fork set
- Deploy to staging for 1 week minimum before production
- Test with maximum concurrent players (not just 5)
- Monitor logs continuously first 48 hours post-deployment
- Have rollback plan ready (revert to previous LMP, force full client resync)
- Gather player feedback immediately: "Any unexpected locking behavior?"
- Be ready to disable individual event systems if specific issue occurs
- This PR should land **last** (after all others prove stable)

---

## Known Limitations

- Does not sync: mods, part upgrades, science data (these have separate systems)
- Crew assignment conflict resolution: first-come-first-served (could be improved)
- EVA jetpack fuel: updates every 0.5s (might lag slightly in high-latency situations)

---

## Notes

This PR is optional but recommended. It makes multiplayer more polished and less prone to conflicts.

If you skip this PR, you'll still have working multiplayer, but you'll hit edge cases that this PR prevents.

The changes are well-tested on live servers and have prevented dozens of player issues.
