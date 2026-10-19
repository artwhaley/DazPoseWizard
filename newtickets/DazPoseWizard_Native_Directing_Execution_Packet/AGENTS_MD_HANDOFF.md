# Root AGENTS.md handoff — active packet without weakening Unity ban

`AGENTS.md` currently points to the previous Studio corrective packet as the active execution. Do **not** let that stale reference cause a run against old R tickets or assume Unity launches are newly authorized. **At N00, as the first local implementation change, update only the top active-execution reference** to the new packet path, after confirming the packet has been installed in-repo or documenting its actual path. Preserve all existing user restrictions and history below the top section. Do not claim that this packet overrides other repository safety requirements; where there is conflict, the **stricter no-Unity rule wins**.

Suggested new top section (adapt only the path to where Art installs the folder):

```md
## Active execution: Native Directing correction (N00–N11)

The current corrective execution is `newtickets/DazPoseWizard_Native_Directing_Execution_Packet/START_HERE.md` and `EXECUTOR_PROMPT.md`, with mandatory `MANUAL_GATE_PROTOCOL.md`. Earlier Studio corrective packet references are historical, not the active implementation.

ABSOLUTE: EXECUTOR MUST NOT RUN OR OPERATE UNITY, including Editor GUI, Unity Hub, CLI/batch/EditMode tests, `-executeMethod`, Play, builds, players, or computer-use. The executor implements and asks Art to open Unity/click diagnostic menus/compile/inspect at gates G1–G4. No automatic gate bypass. Source-only .NET tests/static checks allowed.

Local implementation commits authorized by execution of this packet. No push, merge, PR, or other remote write without separate explicit authorization. Preserve production art, bound source assets and global index. A copied scene alone does not protect shared assets.
```

**If `AGENTS.md` is user-modified or forbids editing its own policy**, stop and ask Art to apply this block. Never overwrite a dirty file without comparing it. If the packet lives outside the repository, describe the accessible actual location instead of inventing a `newtickets/` path. In all cases, the root existing ban remains binding even before this update.
