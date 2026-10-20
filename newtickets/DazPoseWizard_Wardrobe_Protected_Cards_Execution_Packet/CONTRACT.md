# Fixed contract

## C1 — Asset ownership

`SuccubusPerformer` and `PerformerWardrobe` stay on Lara's current root. One real Lara skeleton/body. `WardrobeCatalog`, presets, released fit states and their generation/configuration binding remain authoritative. Reuse the existing wardrobe transaction, effect/footwear rebind and rollback.

A generated `WardrobeRig.prefab` contains `WardrobeRigBinding`, `ClothingRoot`, `HairRoot` and a reference to a generated `WardrobeRigManifest`. The manifest maps released preset IDs to clothing prefabs and stable hair keys to independent hair prefabs. It is derived data, not a second editable catalog. All three currently catalogued outfits are included; no new imported art is required. Instantiate current/target attachments and keep a bounded cache; never instantiate every catalog outfit by default.

Each visual prefab contains `WardrobeAttachmentDescriptor`, renderer parts and accessory/local bones. Bind canonical bones to the owning Lara skeleton; accessory bones stay local to that attachment. Preserve source meshes/materials and their references. No duplicate Lara or generic substitute rig.

Keep existing hair keys (`presetId/pieceId`). New script aliases reference typed prefab assets so authors do not type those keys. `WardrobePreset.HairPrefab` remains serialized legacy data, unused by the new manifest path; do not edit immutable presets to fill it.

## C2 — Script vocabulary

Case-sensitive canonical spelling:

```dazstory
await Lara.SetOutfit(OpeningOutfit)
await Lara.SetHair(OpeningHair)
await Lara.ClearHair()
await Lara.Outfit.RemoveOneLayer()
await Lara.Outfit.AddOneLayer()
```

All are finite, awaited commands. New `SetOutfit` fully dresses that outfit and preserves hair. `SetHair`/`ClearHair` are independent. Existing C# `OutfitAsync` keeps its legacy preset hair-policy behavior through the same queue; expose a new scoped `SetOutfitAsync` that selects Keep hair policy. Resolve the complete target before returning AlreadyEquipped. Do not fork the engine.

Remove highest visible populated layer, add lowest hidden populated layer, skip empty layers, retain remembered outfit when naked. Already naked/fully dressed/no selected outfit are successful no-ops. Do not introduce seven arbitrary independently selectable layer masks.

Visible appearance changes conceal/reveal for 0.25 seconds each; hidden setup commits hidden without revealing. Outfit 1 must remove the dress before panties. Correct candidate layers to dress=2, panties=1, validate the new intermediate fit, publish a new generation, and retain the previous release.

`OpeningOutfit` and `OpeningHair` are source-owned slots in FirstContact.bindings.asset, initially corrected released first-outfit and Emiko. Other aliases such as FrenchMaid are author-chosen slot names, not assets this packet invents.

## C3 — Declaration semantics

`play Name` invokes a declaration by its kind. A sequence may leave lasting presentation changes; **every card restores entrance presentation before returning normally**. Applies to local/public cards and all direct/Director/nested invocation routes. Director accepts cards only; global published files remain card-only; helper sequences are local. Ambiguity is an error, not precedence guessing.

Root setup runs once at root invocation. A helper `play` does not implicitly run a matching setup. A helper sequence inherits an enclosing card context but does not create one. Recursion is bounded across all playable entries. Intro becomes `sequence Intro`; the FirstContact root stays explicit in saved sequence configuration and Studio analysis.

## C4 — Card return

Capture stable presentation controls once at entrance. Pose targets restore with a fixed 0.5-second blend; changed actor/body locations use existing dissolve machinery with exact captured position/rotation endpoints. Camera returns over 0.5 seconds through its actual view owner. Restore its exact fixed quaternion or captured tracking rule/target/settings. Never choose a prettier target. Do nothing for unchanged domains.

Include Lara/player-body transforms, persistent pose/seat modes, visibility, remembered outfit/layers/hair/fit/footwear, exact shader profile, expression, gaze, aura/grip/follow controls reachable through the facade, view ownership, and card-owned subscriptions/consumers. Typed subsystem snapshots own private runtime details. No reflection or entire-GameObject serialization.

Ambient breathing/blink/idle time and live head input continue. User tuning/configuration/assets are preserved, never lifecycle-reset. Gameplay state (progress, RNG, mood, user clothing/equipment/profile decisions) is not rolled back. Presentation-derived posture observations are kept consistent with restored presentation.

Normal completion requires finite body tasks settled, only card-owned temporary/persistent writers released, return target prepared, return settled/verified, parent ownership reinstated, and only then completion/progress/next cue. Nested cards restore the immediate parent's entrance. No opt-out flags.

Stop cancels immediately and is terminal. Never restore in an uncancelled finally or restart outputs. Fault/restore failure terminates with source/domain diagnostics, no progress and no next card. A destroyed entrance target is an error, never a substitute target. Card exit is not PerformanceRun.Stop/Dispose.

## C5 — Studio

Keep the single wrapped left-aligned cue list and adaptive inspector. Add typed outfit/hair drag targets, inherited appearance and layer descriptions there. No runtime-replay navigation, bindings tab, scenario controls or permanent hair button.

Static card output is identity for protected presentation fields; sequence output composes its changes. Analyze internal card cues under incoming contexts without executing choices/RNG/loops/Director. Unknown entrances remain unknown inside the card. A card-only skipped choice/loop does not contaminate the following presentation; direct persistent sequence writes still can.

Show a stable virtual `Return to card entrance` cue. It projects the endpoint, never runs an exit animation. Preview uses the shared prepared selection and binder under the native lease, never starts Play-only wardrobe. Restore temporary mesh/skin/blendshape/material/MPB/attachment state before save, Stop, reload, Play or disposal. Preserve intentional slot/mark edits and conflicting external edits.

No attachment rebuild, fit preparation or pose resampling on a pure mark update. Targets: lookup <10 ms; warm projection/spatial update <50 ms on Art's project; no increase with cue number. Report actual native timings separately from synthetic checks. Cold work is separately counted and cached.

## C6 — Native boundaries and migration

Art alone operates Unity. Four native gates A–D are mandatory; no source-only certification. Executors prepare concrete saved scenes, menus, controls, reports and exact steps before requesting a gate. No screenshots.

Allowed new generated roots: `Assets/Production/Wardrobe/Rigs/<generation>/`, `Assets/__WardrobeProtectedCards/`, and new uniquely named generation directories created by the existing publisher. Old release/config art remains immutable. Diagnostics do not apply prefab overrides to source assets. Production catalog/config and Lara mount advance together only in W09 after A–C pass, with exact before/after references recorded.

Production changes are limited to FirstContact.dazstory and its owned bindings, the wardrobe references/mount on Lara and any necessary dependency declarations. Preserve all 19 fixed dialogue transcripts/audio, environment, source GUIDs, named marks, player prefab and measured contact setup. Global source membership stays the same; content fingerprint can refresh after authorized source/schema changes and must be recorded.

No Unity/native tests/builds/hardware by the executor, no remote writes, no subagents. Packet preparation itself does not authorize implementation or commits.
