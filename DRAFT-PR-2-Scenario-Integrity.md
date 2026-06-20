# PR #2: Scenario Data Integrity & Migration

**Target:** Upstream LunaMultiplayer/LunaMultiplayer master  
**Status:** DRAFT  
**Type:** Data Integrity + Migration  
**Complexity:** ⚠️⚠️ MEDIUM-HIGH  
**Risk Level:** MEDIUM (needs regression testing)

---

## Files Changed (10 files, ~882 lines)

| File | Changes | Purpose |
|------|---------|---------|
| `Server/System/Scenario/ScenarioContractsMigration.cs` | +155 | NEW: Contract version upgrade and data migration logic |
| `Server/System/Scenario/ScenarioContractsDataUpdater.cs` | +169 | Major: Contract data sync, version upgrades, migration coordination |
| `Server/System/Scenario/ScenarioAchievementsCrewDedupe.cs` | +109 | NEW: Crew achievements deduplication for conflict resolution |
| `Server/System/Scenario/ScenarioAchievementsDataUpdater.cs` | +6 | Hook for achievements dedup integration |
| `ServerTest/ScenarioAchievementsCrewDedupeTest.cs` | +242 | NEW: Unit test suite for dedup logic |
| `LmpClient/Systems/ShareAchievements/ShareAchievementsMessageSender.cs` | +53 | Enhanced achievements sync with dedup awareness |
| `LmpClient/Systems/ShareContracts/ShareContractsEvents.cs` | +24, -11 | Contract event handler improvements |
| `LmpClient/Systems/ShareContracts/ShareContractsMessageHandler.cs` | +30 | Contract message reception and validation |
| `LmpClient/Systems/ShareContracts/ContractPartReferenceChecker.cs` | +47 | NEW: Validate contract part references |
| `LmpClient/Systems/ShareContracts/ContractsScenarioSanitizer.cs` | +78 | NEW: Sanitize contract scenario data |

---

## Summary of Changes

### ScenarioContractsMigration.cs — Data Upgrade Logic (NEW)
- **Purpose:** Handle version-to-version contract schema upgrades
- **Use Case:** Long-running servers accumulate contract data across multiple LMP versions; this ensures backward compatibility
- **Features:** 
  - Detects contract format version
  - Applies migration steps sequentially
  - Logs all transformations for debugging

### ScenarioContractsDataUpdater.cs — Contract Data Synchronization (MAJOR)
- **Purpose:** Sync contract state between server and clients during scenario saves
- **Features:**
  - Detects breaking changes in contract definitions
  - Triggers migration if version mismatch
  - Handles partial contract failures gracefully
  - Validates contract integrity post-migration

### ScenarioAchievementsCrewDedupe.cs — Crew Deduplication (NEW)
- **Problem:** Crew members respawn/get deleted/rejoin, creating duplicate achievement records
- **Solution:** Detect duplicate crew entries and merge their achievements
- **Use Case:** On mature saves (1000+ hours), crew dedup prevents sync conflicts
- **Features:**
  - Compares crew by UUID and name
  - Merges achievement records
  - Removes orphaned entries
  - Logs all dedup actions

### ShareAchievementsMessageSender.cs — Dedup-Aware Sync
- **Change:** Send achievements with dedup metadata
- **Impact:** Clients aware of merged records, prevents re-divergence

### ContractPartReferenceChecker.cs — Contract Validation (NEW)
- **Purpose:** Verify contract part references are valid
- **Use Case:** Prevents contracts from referencing deleted parts
- **Features:**
  - Scans all active contracts
  - Validates part GUIDs against part catalog
  - Flags or removes invalid references

### ContractsScenarioSanitizer.cs — Data Cleanup (NEW)
- **Purpose:** Fix corrupt scenario data before syncing
- **Use Case:** Malformed contract data from crashes or bugs
- **Features:**
  - Validates field types
  - Removes null/invalid entries
  - Normalizes enums
  - Logs all sanitizations

### Scope boundary note
- `LmpCommon/Message/Data/Vessel/VesselProtoMsgData.cs` is tracked under **PR #3** as the primary vessel-proto message architecture change.
- PR #2 consumes that message shape but does not own the file for accounting purposes.

---

## PR Description

After running 1000+ hour multiplayer saves, we observed scenario data drift over time. Crew respawn, die, and rejoin, which can create duplicate achievement records. Different clients then sync against different crew records and produce conflicting achievement data. Contracts can also reference parts that no longer exist, and older saves can carry contract fields that no longer match current schema.

This PR adds data cleanup and migration:

**Contract Migration:** When a save from an older LMP version loads, detect it and apply schema updates. Runs once at server startup, no player impact. Old contracts just work.

**Achievements Dedup:** Crew duplicates mess with sync. We detect them (same person, two records) and merge their achievements. No more clients getting conflicting data because they're syncing against different crew records.

**Contract Validation:** Before syncing, verify part references are real. If they're not, we flag or remove them. Contract still works, just without the broken part reference.

**Scenario Sanitization:** Fix corrupted data (wrong types, nulls, broken enums). Keep it consistent.

All this runs on server load, not per-message. The dedup logic is fully tested. Clients see clean data, never know any of this happened.

---

## FOLLOW-UP COMMENT (Post 2 minutes after PR creation)

**Testing Plan:**

1. **Unit Tests (Required):**
   - Run `ServerTest/ScenarioAchievementsCrewDedupeTest.cs`
   - All 14 tests must pass
   - Covers: empty crew, circular references, name/UUID conflicts, partial duplicates, achievement merging

2. **Integration Test (Load a 500+ hour save):**
   - Load a real long-running save with known crew duplicates
   - Verify dedup runs at startup (check logs)
   - Verify duplicate crew records merged into single record
   - Verify achievement counts correct after merge
   - Verify no data loss in merging process

3. **Contract Migration Test:**
   - Load a save from LMP 0.28.x (older contract schema)
   - Verify migration runs silently at startup
   - Verify contracts load and are playable (can complete them)
   - Verify no "contract schema mismatch" errors in logs

4. **Validation & Sanitization Test:**
   - Load a save with known corrupt contracts (missing parts, bad enums)
   - Verify sanitization logs what it fixed
   - Verify contracts are playable after cleanup
   - Verify bad references were handled gracefully (removed, not crashed)

5. **Regression Test (10 diverse saves):**
   - Load 10 different saves (1-hour fresh, 10-hour mid-game, 100-hour mature, 500+ hour long-term)
   - For each: verify no achievements/contracts lost, no exceptions, dedup logic completes
   - Monitor memory during load: should not spike
   - Monitor load time: should not increase significantly

6. **Live Server Test (48 hours):**
   - Deploy to staging with cloned production saves
   - Monitor logs for dedup/migration/sanitization activities
   - Verify clients sync achievements correctly
   - Verify no sync conflicts after dedup
   - Have 5+ players connect and play, verify no strangeness

---

## COMMITS & CONTRIBUTORS

Upstream commits this work is based on:

- `0fb53563` (AdmiralRadish, upstream/master): scenario parsing hardening and contract data handling
- `b1ad4cd0` (Drew Banyai, upstream/master): career contract synchronization fix
- `0c37cb20` (Drew Banyai, upstream/Release/0_29_2): release-branch contract duplication/missing fix
- `48197209` (Drew Banyai, upstream/Release/0_29_2): release-branch contract sanitization for unloadable parts
- `0e11c918` (Drew Banyai, upstream/Release/0_29_2): achievements crew dedupe carryover

Fork integration in this PR:

- `ScenarioContractsMigration.cs`, `ScenarioAchievementsCrewDedupe.cs`: fork-side migration/dedupe flow
- `ScenarioContractsDataUpdater.cs`: migration + sync coordination
- `ServerTest/ScenarioAchievementsCrewDedupeTest.cs`: coverage for dedupe edge cases
- ShareContracts/ShareAchievements sender/handler updates and sanitizers

---

## RISKS

**Risk Level:** ⚠️⚠️ MEDIUM

**What could go wrong:**
- **Data Loss:** Dedup/migration logic runs once on load. If it's wrong, you lose achievement/contract data permanently. Mitigated by: full test suite, logging every change, rollback creates new saves.
- **Unexpected Crashes:** Edge cases in old saves we haven't seen. Mitigated by: defensive error handling, logging before any destructive operation.
- **Performance Impact:** Dedup/migration happens at startup. Could slow down server load for large saves. Mitigated by: only runs once, no runtime impact.
- **Sync Issues:** After dedup, clients might get stale cached achievement data. Mitigated by: achievements are re-sent to clients after dedup.

**Mitigation strategy:**
- Always back up save files before deploying
- Test on staging with production save clones
- Monitor logs during initial load on production
- Have rollback plan ready (previous LMP version + original save)
- Deploy during off-peak hours first time

---

## Risk Assessment

**Risk Level:** ⚠️⚠️ MEDIUM

**Why Medium Risk:**
- Modifies scenario save data (migration is irreversible)
- Affects contract and achievements state (could lose data if migration logic is wrong)
- Must handle edge cases (partially corrupted saves, missing data)

**Mitigations:**
- All operations are logged; admins can audit what changed
- Migration is tested on real-world saves (we have samples)
- Dedup logic has unit test coverage
- Sanitization is conservative (only removes provably invalid data)

**Recommendation:** Test on staging server with cloned production saves before deploying to live.

---

## Notes for Reviewer

This is our solution to the "long-running server stability" problem. After 1000+ hours, raw scenario data needs maintenance.

The dedup logic is the most complex part—review that carefully. The test suite is exhaustive and tests edge cases (empty crew, circular references, etc.).

Contract migration is straightforward (version detection → conditional logic). Similar pattern to database migrations.

If upstream prefers a different approach to dedup (e.g., per-player sync instead of global dedup), happy to refactor. The important part is that the problem gets solved.
