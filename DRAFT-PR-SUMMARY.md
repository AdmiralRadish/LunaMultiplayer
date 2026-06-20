# Draft PR Summary — All 10 PRs

**Created:** May 27, 2026  
**Status:** Ready for Review  
**Total Files Changed:** 68  
**Total Lines Added:** +5,440, -457

---

## Quick Reference Table

| # | Title | Target | Files | Lines | Risk | Priority |
|---|-------|--------|-------|-------|------|----------|
| 1 | Network Robustness | ✅ Upstream | 4 | ~280 | ✅ None | P0 |
| 2 | Scenario Integrity | ✅ Upstream | 10 | ~882 | ⚠️ Medium | P0 |
| 3 | Vessel Sync Overhaul | ✅ Upstream | 15 | ~1,516 | ⚠️⚠️ High | P0 |
| 4 | Server Infrastructure | 🔒 Fork | 10 | ~362 | ✅ Low | P1 |
| 5 | Diagnostics Tools | 🔒 Fork | 4 | ~925 | ✅ None | P2 |
| 6 | Harmony Patches | 🔒 Fork | 5 | ~408 | ✅ Low | P2 |
| 7 | Build Automation | 🔒 Fork | 2 | ~477 | ✅ None | P3 |
| 8 | Configuration Updates | 🔒 Fork | 7 | ~98 | ✅ Low | P2 |
| 9 | Scene/Events Systems | 🔒 Fork | 10 | ~265 | ⚠️ Medium | P1 |
| 10 | Warp Allotment Tracking | 🔒 Fork | 1 | ~264 | ✅ Low | P2 |

---

## Upstream PRs (3 for LunaMultiplayer/LunaMultiplayer)

### ✅ PR #1: Network Robustness & Startup Optimization
- **Goal:** Harden server against network chaos, optimize startup
- **Value:** ⭐⭐⭐⭐⭐ HIGH
- **Risk:** ✅ NONE
- **Timeline:** 1-2 days review
- **Start Here:** YES
- **Files:** DRAFT-PR-1-Network-Robustness.md

### ✅ PR #2: Scenario Data Integrity & Migration
- **Goal:** Long-running server support (contracts, achievements, crew)
- **Value:** ⭐⭐⭐⭐ HIGH
- **Risk:** ⚠️ MEDIUM
- **Timeline:** 3-5 days review + testing
- **Start After:** PR #1 accepted
- **Files:** DRAFT-PR-2-Scenario-Integrity.md

### ✅ PR #3: Vessel Synchronization Architecture Refactoring
- **Goal:** Solve scaling issues with large vessels, long campaigns
- **Value:** ⭐⭐⭐⭐⭐ HIGHEST
- **Risk:** ⚠️⚠️ HIGH
- **Timeline:** 1-2 weeks review + extensive testing
- **Start After:** PR #1 & #2 accepted
- **Files:** DRAFT-PR-3-Vessel-Sync-Overhaul.md

---

## Internal Fork PRs (7 for AdmiralRadish/LunaMultiplayer)

### 🔒 PR #4: Server Infrastructure & Lifecycle Management
- **Goal:** Keep servers running stable 24/7
- **Value:** ⭐⭐⭐ MEDIUM
- **Risk:** ✅ LOW
- **Timeline:** 1 day
- **Depends On:** PR #8 (Configuration Updates)
- **Files:** DRAFT-PR-4-Server-Infrastructure.md

### 🔒 PR #5: Diagnostics & Performance Profiling Tools
- **Goal:** Debug sync issues without code changes
- **Value:** ⭐⭐⭐ MEDIUM (ops/debug only)
- **Risk:** ✅ NONE
- **Timeline:** 1 day
- **Depends On:** PR #8 (Configuration Updates)
- **Files:** DRAFT-PR-5-Diagnostics-Profiling.md

### 🔒 PR #6: Harmony Engine Patches
- **Goal:** Fix KSP engine edge cases in multiplayer+RSS
- **Value:** ⭐⭐⭐ MEDIUM
- **Risk:** ✅ LOW
- **Timeline:** 1 day
- **Depends On:** None
- **Files:** DRAFT-PR-6-Harmony-Patches.md

### 🔒 PR #7: Build Automation & Server Deployment
- **Goal:** Automate release packaging and server startup
- **Value:** ⭐⭐⭐ MEDIUM (devops only)
- **Risk:** ✅ NONE
- **Timeline:** 1 day (no testing needed)
- **Depends On:** None
- **Files:** DRAFT-PR-7-Build-Automation.md

### 🔒 PR #8: Configuration & Message Data Updates
- **Goal:** Support new features in other PRs
- **Value:** ⭐⭐ LOW (infrastructure)
- **Risk:** ✅ LOW
- **Timeline:** 1 day
- **Depends On:** None
- **Required By:** PR #4, #5
- **Files:** DRAFT-PR-8-Configuration-Updates.md

### 🔒 PR #9: Scene Management & Event System Enhancements
- **Goal:** Prevent multiplayer conflicts in game systems
- **Value:** ⭐⭐⭐ MEDIUM
- **Risk:** ⚠️ MEDIUM
- **Timeline:** 2-3 days
- **Depends On:** None
- **Files:** DRAFT-PR-9-Scene-Events-Systems.md

### 🔒 PR #10: Warp Allotment Tracking & Persistence
- **Goal:** Track per-player cumulative server-time warp allotments
- **Value:** ⭐⭐ LOW-MEDIUM (ops/accounting)
- **Risk:** ✅ LOW
- **Timeline:** 1 day
- **Depends On:** None
- **Files:** DRAFT-PR-10-Warp-Allotment-Tracking.md

---

## Recommended Landing Order

### Phase 1: Upstream (Week 1-4)
1. **PR #1: Network Robustness** ✅ (1-2 days)
   - Land immediately, no blockers
   - After accept: proceed to Phase 2

2. **PR #2: Scenario Integrity** ⏳ (3-5 days)
   - After PR #1 accepted
   - Builds credibility

3. **PR #3: Vessel Sync Overhaul** ⏳ (1-2 weeks)
   - After PR #1 & #2 accepted
   - Largest PR, heaviest review

### Phase 2: Internal Fork (Week 1-2, parallel to upstream)
1. **PR #8: Configuration Updates** ✅
   - No dependencies, land first
   - Required by PRs #4 & #5

2. **PR #4: Server Infrastructure** ✅
   - After PR #8 (optional dependency)
   - Can land independently

3. **PR #5: Diagnostics Tools** ✅
   - After PR #8 (optional dependency)
   - Useful but not critical

4. **PR #6: Harmony Patches** ✅
   - No dependencies, land anytime
   - RSS-specific, stable

5. **PR #7: Build Automation** ✅
   - No dependencies, land anytime
   - Devops only

6. **PR #9: Scene/Events Systems** ⏳
   - No dependencies but medium risk
   - Land after PR #4, #5 are stable

7. **PR #10: Warp Allotment Tracking** ✅
   - Independent operational feature
   - Can land any time after PR #4

---

## Review Checklist

### Coverage Audit (June 3, 2026)
- Source of truth diff: `git diff --name-status upstream/master...0_29_3`
- Result: **68 files changed** (`A=17`, `M=51`, `D=0`)
- PR ownership audit result: **68/68 files assigned**, **0 unassigned**, **0 multi-assigned**
- Merge sanity result: `0_29_3` already contains both `upstream/Release/0_29_2` and `upstream/master` (both merge steps now "Already up to date")
- Schema sanity result: serialized vessel message changes are additive trailing fields (`Reason`) with backward-compatible reads in `VesselProtoMsgData` and `VesselRemoveMsgData`; quaternion deserialize fix in `VesselPartSyncFieldMsgData` is wire-format neutral.

Single-owner corrections applied in this pass:
- `LmpCommon/Message/Data/Vessel/VesselProtoMsgData.cs` is owned by **PR #3** (removed from PR #2 scope list)
- `Server/Settings/Definition/IntervalSettingsDefinition.cs` is owned by **PR #4** (removed from PR #8 scope list)

Before sharing with upstream/team:

### PR #1: Network Robustness
- [ ] Read DRAFT-PR-1-Network-Robustness.md
- [ ] Agree with exception handling approach
- [ ] Approve Prewarm optimization
- [ ] Check EntryPoint.cs integration looks good

### PR #2: Scenario Integrity
- [ ] Read DRAFT-PR-2-Scenario-Integrity.md
- [ ] Review dedup logic (most complex part)
- [ ] Verify test suite coverage adequate
- [ ] Confirm migration logic handles edge cases

### PR #3: Vessel Sync Overhaul
- [ ] Read DRAFT-PR-3-Vessel-Sync-Overhaul.md
- [ ] Carefully review VesselLoader.cs checkpoint logic
- [ ] Understand state machine in VesselProtoSystem.cs
- [ ] Verify LocalTopologyTracker algorithm
- [ ] Plan testing strategy (most important PR)

### PRs #4-10: Internal Fork
- [ ] Read all summaries
- [ ] Verify no conflicts with your fork
- [ ] Identify any RSS-specific issues
- [ ] Plan integration into your workflow

---

## Key Decision Points

**Question 1: Upstream Strategy**
- Propose all 3 upstream PRs? (Recommended)
- Propose only #1 & #2, hold #3? (Conservative)
- Propose only #1? (Very conservative)

**Question 2: Fork Integration**
- Land all 7 internal PRs? (Recommended)
- Land only core 3 (#4, #8, #9)? (Minimal)
- Land only diagnostics? (Ops-focused)

**Question 3: Timeline**
- All PRs by June 30? (Aggressive)
- All PRs by July 31? (Reasonable)
- Spread over Q3? (Conservative)

---

## Integration Risks

### Upstream Risks
- PR #1: Minimal risk, likely accepted
- PR #2: Medium risk, may request changes
- PR #3: High risk, may take weeks to review

**Mitigation:** Land #1 first to build credibility, then #2, then #3

### Internal Fork Risks
- PRs #4-10 are well-tested, low risk
- PR #9 has medium risk but is optional
- PRs interact but can be landed independently

**Mitigation:** Land in suggested order, test each before next

---

## Next Steps

1. **Review** all 10 DRAFT-PR-*.md files in LunaMultiplayerRSS-source
2. **Decide** which upstream PRs to propose (#1 only? #1+#2? #1+#2+#3?)
3. **Plan** internal fork integration (all 7 or subset?)
4. **Schedule** review sessions with team if needed
5. **Validate** testing plans are adequate
6. **Execute** landing in recommended order

---

## Files in This Review

- DRAFT-PR-1-Network-Robustness.md
- DRAFT-PR-2-Scenario-Integrity.md
- DRAFT-PR-3-Vessel-Sync-Overhaul.md
- DRAFT-PR-4-Server-Infrastructure.md
- DRAFT-PR-5-Diagnostics-Profiling.md
- DRAFT-PR-6-Harmony-Patches.md
- DRAFT-PR-7-Build-Automation.md
- DRAFT-PR-8-Configuration-Updates.md
- DRAFT-PR-9-Scene-Events-Systems.md
- DRAFT-PR-10-Warp-Allotment-Tracking.md
- DRAFT-PR-SUMMARY.md (this file)
- FORK-DIVERGENCE.md (complete file listing)

---

## Questions for Review

1. **Strategic:** Should we propose #3 to upstream or keep it fork-exclusive?
2. **Technical:** Is the Prewarm optimization in PR #1 worth the added complexity?
3. **Risk:** Is the dedup logic in PR #2 comprehensive enough for all edge cases?
4. **Testing:** What additional testing is needed before landing internally?
5. **Timing:** When should we start the upstream PR process?

---

**Status:** ✅ Ready for review. All 10 PRs drafted and linked.
