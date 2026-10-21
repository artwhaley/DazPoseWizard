# Wardrobe in First Contact

## Return to authoring

1. Exit Play and open **Assets/Scenes/FirstContact.unity**. If Unity asks to save the retired Gate B scene, choose **Don't Save** for that diagnostic scene.
2. Use **Assets > Refresh** and wait for compilation/import.
3. Open **DazPose > Story > Scene Director Studio**. If needed expand **Scene / script** and click **Find open scene**.
4. Search **Camera.Track** in the cue list, select **Camera.Track(Lara)** in **Intro**, and choose **After this cue**. The opening visible look is outfit 1 and Emiko hair, using the actual script's camera rule.
5. Clear search to continue cue-by-cue authoring. The right inspector exposes current/inherited outfit and hair slots alongside pose and mark controls. Drag a released WardrobePreset into **OpeningOutfit**, or a generated Hair prefab root into **OpeningHair**, to change those named assets. Binding changes use normal Undo/save.

Use the actual saved First Contact scene for Play. No diagnostic preparation/release is needed to use these existing assets. Stop preview restores source presentation. Native use of this new delivery remains Art-operated; source compilation is not visual approval.

## Delivered script commands

```text
await Lara.SetOutfit(OpeningOutfit)
await Lara.SetHair(OpeningHair)
await Lara.ClearHair()
await Lara.Outfit.RemoveOneLayer()
await Lara.Outfit.AddOneLayer()
```

SetOutfit selects clothing at full layers and preserves hair. SetHair uses a source-owned generated Hair prefab slot. Remove takes the highest visible populated layer; Add restores the lowest hidden layer. The extremes are no-ops. Outfit 1 has dress at layer 2 and panties at layer 1: remove dress, then panties; add panties, then dress.

The two opening commands are already in **sequence Intro**, after hidden setup/idle and before the first reveal. OpeningOutfit points to the accepted first-outfit release; OpeningHair points to Emiko. Production Lara references the matching catalog/config/manifest.

Cards restore entrance presentation; sequences retain changes. The virtual **Return to card entrance** Studio row previews that boundary without executing story choices/variables. Skipped logic that makes persistent appearance ambiguous is labelled.

## Reuse and cleanup

Future outfits/hair use the existing importer, configuration/fit compiler, immutable publication and **DazPose > Wardrobe > Appearance Delivery** visual-asset generator. No per-outfit fixture step was added. Gate B scene/setup/runner are retired; recoverable copies are outside Assets in ignored .dazposewizard/wardrobe-retired/20261020-GateB.
