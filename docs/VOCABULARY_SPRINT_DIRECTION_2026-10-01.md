# Vocabulary Sprint — Tentative Direction

**Date:** October 1, 2026  
**Status:** Tentative planning record

This sprint expands the performer's usable vocabulary after the accepted **First Contact** milestone. It is **not a batch implementation plan**. Each capability should be designed, implemented, tested, and accepted individually when explicitly directed.

The governing rule remains: expose a small semantic command and keep implementation machinery behind that seam.

## Proposed vocabulary

1. **`TeleportTo(target[, arrivalPose])`** — Finite magical relocation to an exact destination. An optional arrival pose is applied through the existing persistent `Pose` path while Lara is concealed; teleportation does not invent a second pose owner.

2. **`DissolveTo(target[, arrivalPose])`** — Finite particle-dissolve relocation. Lara dissolves, an independent world-space cloud travels from origin to destination, Lara's root relocates while hidden, optional arrival pose is applied through `Pose`, then she reforms.

3. **`TurnTo(target)`** — Finite whole-body/root reorientation. Distinct from persistent head/eye `LookAt`.

4. **`Emphasize()`** — Short semantic punctuation animation layered over the persistent body state. Intended as an easy authoring verb, backed by the generic one-shot gesture mechanism.

5. **`Gesture(gesture)`** — General one-shot authored gesture overlay such as beckon, point, or blow-kiss without replacing the persistent pose.

6. **`Cast(spell[, target])`** — Finite spell performance coordinating authored animation/gesture, VFX, sound, timing, and optional target semantics. A spell is a focused asset, not a universal gameplay-event framework.

7. **`Aura(aura)` / `ClearAura()`** — Persistent magical visual state, conceptually parallel to `Pose` and `Expression`.

8. **`ChangeOutfit(outfit)`** — Finite/state transition for changing Lara's clothing presentation. Exact wardrobe implementation remains open until this item is taken up.

9. **`Stroke(motionPlayback)`** — Lara consumes a shared normalized motion source to drive procedural hand motion. The public concept is motion, not the funscript file format.

10. **`StopStroke()`** — Gracefully stop/release the procedural stroke action without exposing its internal animation machinery.

11. **`Player.Toys.Play(motionPlayback)`** — A player-owned Bluetooth toy consumes the same authoritative motion playback used by Lara. Toys belong under the Player hierarchy, not under `SuccubusPerformer`.

12. **`Player.Toys.SetLevel(value)` / `Player.Toys.Stop()`** — Direct persistent/control vocabulary for toy output when playback is not driven by a motion program.

## Shared motion synchronization

Synchronization must be based on **one authoritative playback clock**, not on starting two independent timelines in the same frame.

A `MotionPlayback` owns the program and current authoritative time and surfaces the current normalized sample (at minimum position 0..1; velocity/direction may also be useful). Lara's procedural animation and the Player's Bluetooth toy layer are independent consumers of that same playback.

Each consumer samples where the shared playback is **now**. Unity frame hitches or a toy's lower transport/update rate therefore do not create accumulating timeline drift. Device-specific latency compensation can later be applied as calibration against the same clock.

A funscript is one possible source format for a `MotionProgram`; procedurally generated curves or other authoring formats should be able to feed the same playback seam.

## Teleport/dissolve pose ownership

Both relocation verbs must support changing body pose while Lara is fully concealed. Example intent:

```csharp
await Lara.DissolveToAsync(PlayerCloseMark, OnAllFours);
```

The relocation command coordinates the hidden timing, but the arrival state must become Lara's ordinary persistent `Pose`. There should be no teleport-owned or dissolve-owned pose layer left behind after arrival.

## Scope discipline

This sprint expands **performance vocabulary**. It does not yet generalize First Contact into a director, invent the final authoring syntax, or build personality, memory, moods, or broader gameplay systems. Those belong to the later authoring/storytelling sprint.

Implement these capabilities **one at a time as directed**, validating the seam and the subjective result before moving to the next item.
