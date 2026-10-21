# S05 — Typed binding UI and contextual media preview

**Depends:** S04. **Goal:** binding is visual asset selection/preview, not hand-entered GUIDs.

Right pane reflects selected source verb: typed ObjectField and drag/drop for `PerformerPose` (generated wrapper only), Gesture, `AudioClip`, shader visual profile, funscript, existing scene Transform/Seat/target; asset browser search filtered to accepted types. Incorrect `.anim` into `PerformerPose` shows actionable import/wrapper guidance; no silent conversion or wrong type acceptance. General pose property shows configured `World.Posture`, author may explicitly change it via validated UI. Audio waveform/transcript, click to play/stop preview clip, show transcript mismatch if changed. Gesture/action preview uses true clip; funscript shows position-vs-time curve and does not talk to hardware; magic effect previews actual profile if safe and restores preview materials. Add issue banner for unbound fields. Save explicit content choices to source-owned companion through Undo, serialized object/property APIs and guarded reimport; preserve assignment on source movement/reconciliation. Scene marks remain S06.

Tests: assign/reassign/Undo pose, audio, magic, funscript in temporary source; wrong type rejects; source edit updates dialogue key; unchanged asset doesn't rewrite; FirstContact current six poses/19 WAVs remain bound after Studio open.

**Gate:** Author can complete new script content bindings entirely with typed drag/drop; sees actual Lara pose/audio playback; generic editor no "First Contact" label.
