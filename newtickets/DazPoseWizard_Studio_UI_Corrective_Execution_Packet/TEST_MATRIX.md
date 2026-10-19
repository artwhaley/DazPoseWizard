# Required verification matrix

All must have independent proof; add tests to existing suites rather than inventing a new test framework. Unity-specific tests may compile and remain *unexecuted* if forbidden by the manual-testing boundary. Every skipped native gate is clearly marked.

| ID | Scenario | Passing observation |
|---|---|---|
| T01 | 100 interests, scroll to row ~73, change frequency | Row remains visible, anchor and focus stable, edit draft correct |
| T02 | 100 equipment, row ~80, owned/reward/punishment | No jump-to-top; independent toggles; Apply/Cancel unchanged |
| T03 | Search/filter membership changes after edit | Predictable nearest-item anchor; changing query explicitly starts at top |
| T04 | UI row recycled during scroll | No stale callback changes prior catalog ID; keyboard focus recoverable |
| T05 | Fresh .dazstory unbound import | Syntactically compiled, typed manifest, companion auto-owned by source GUID |
| T06 | Legacy companion fully populated, `recordUndo=false` | GUID ownership backfilled without modifying assigned assets |
| T07 | Reconcile twice no differences | No asset dirtying/reimport loop or byte changes after first convergence |
| T08 | Rename/move .dazstory, leave companion in old location | Same bindings found via GUID, valid compilation, no silent duplicate or data loss |
| T09 | Duplicate source with incompatible companion; broken syntax | Clear diagnostic, no blind asset overwrite or data deletion |
| T10 | Warmup draw at same PC 3 times, force only second | Visit #1 seeded; #2 forced; #3 seeded; exactly one selection RNG draw each |
| T11 | Repeated if/loop branch same PC, force only chosen visit | Other visits computed normally; saved scenario replay stable |
| T12 | Repeated race/wait site; choose one visit | Chosen event only on that visit; others prompt/simulate normally |
| T13 | Ineligible forced card | Refused with canonical reason; no card executes or progress credited |
| T14 | `Back` and Reset/replay | Same program + scenario reproduces visit IDs and choices; no stale overrides |
| T15 | Edit root source mid-rehearsal | Program/preview marked stale; stopped; cannot advance until rebuild |
| T16 | Change bound audio/pose, global card index, host, or saved mark | Correct invalidation; no source/scene/profile accidental writes |
| T17 | Camera.StartAt → Track(Lara) → Lara moves twice | Player-camera preview remains aimed at moving Lara after both moves |
| T18 | Orbit vs Player camera and sample pose | Orbit remains independently navigable; tracking updates in player-camera view |
| T19 | Click pose in source after locomotion | Correct resulting pose **at** authored walk destination, not origin/default |
| T20 | Select branch-dependent line | Follows seed/scenario or explicitly asks for branch choice; no invented path |
| T21 | Draw global card; navigate into card body | Correct source file, occurrence, content owner, binding and staged result |
| T22 | 0/2/6 clothing and both climax event races | Full First Contact virtual traces unchanged except deliberate selection UI behavior |
| T23 | Global tagged card add/remove without scene edits | Added matching card appears/disappears in Director candidate pool; no scene library registration |
| T24 | Cancel/Stop/reload Editor window mid-transport | No orphan tasks/listeners, audio preview/AnimationMode leaks, serialized mutations |

## Baseline and regression commands (adjust only to actual installed tooling)

```bash
dotnet test tests/DazPose.Story.Tests/DazPose.Story.Tests.csproj --no-restore
dotnet test tests/DazPose.Player.Tests/DazPose.Player.Tests.csproj --no-restore
dotnet test tests/DazPose.Rehearsal.Tests/DazPose.Rehearsal.Tests.csproj --no-restore
dotnet test tests/DazPose.Tests/DazPose.Tests.csproj --no-restore
python scripts/check-unity-source.py
python scripts/audit_p2_p3_assets.py
git diff --check
```

Some tests need a previous restore; document if dependencies unavailable. The audit may write an ignored evidence artifact. Source compile is **not** native Unity Editor import. Evidence must distinguish executed PASS, blocked/not run, and user pending. Protect existing First Contact source/companion GUID/serialized references; if a legitimate one-time GUID ownership migration changes companion bytes, report only that deliberate delta, preserving all referenced pose/audio/material fields.
