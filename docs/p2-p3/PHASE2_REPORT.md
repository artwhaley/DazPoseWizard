# Phase 2 player UI status

**Status: PARTIAL; U07 technical gate not certified.** Source and offline serialized-asset checks cover the Main Menu integration and shared UI foundation. Unity compilation, Edit Mode tests, rendered screenshots and user Play/visual review remain pending. The user’s manual-testing boundary was respected: no Unity, Play, build or app launch was run.

## Implemented and statically checked

The saved `Assets/Production/Scenes/MainMenu.unity` uses the `ProductionUiLibrary` MainMenu template while retaining its existing DazPose application object and Event System. MainMenu initialization applies the theme before binding the public `SceneCatalog`. Scene selection is virtualized; unavailable catalog paths display a reason and disable Start; Settings and Quit are available. Settings errors persist until another action, keyboard focus begins on the scene list, opens on a Settings control, and returns to the Settings button after close. Escape closes an active modal before it can close Settings.

The reusable profile presenters are reached through the existing Settings screen and the story prompt service. Interests support search, theme/category filters, explicit NO/1x/2x/4x and Loved/Neutral/Punishment controls. Equipment supports search, filters, and independent owned/willing, reward and punishment toggles. Clothing remains a transient story/world-state prompt. Draft profile edits are committed by Apply and discarded by Cancel through the existing edit-session APIs. The Settings prefab retains its endpoint, connect/disconnect/scan/stop, route and volume controls; its redundant overlay Canvas and GraphicRaycaster were removed so it inherits the caller’s Canvas scaling and raycaster. The profile-selection path itself contains no toy-output call; this is source inspection, not runtime verification.

The generated UI prefabs include CanvasRenderer components for their Image/TMP graphics and use the installed TMP font and shader references. The modal backdrop blocks interaction outside dialog controls while leaving the top 6% (64 logical px at the 1080 reference height) clear for the existing Stop control in the Controls top bar. These are serialized/source findings only.

The Main Menu pane anchors calculate to list/detail bounds of x=51–486/525–1,229 and y=101–612 at 1280×720; x=77–730/787–1,843 and y=151–918 at 1920×1080; and x=102–973/1,050–2,458 and y=202–1,224 at 2560×1440. At 1280×800 (16:10), the same panes calculate to y=112–680. This is anchor arithmetic only and does not establish rendered fit, readable typography, or input behavior.

Offline checks found no duplicate document IDs or unresolved local fileID references in the eight generated prefabs, saved MainMenu, SettingsPanel or PerformanceControls prefab. All generated Image/TMP graphics have a CanvasRenderer. MainMenu contains one controller, one SceneBrowserView and one Event System. SettingsPanel no longer declares its own Canvas or GraphicRaycaster; PerformanceControls has no references to the removed component IDs. The UI generator is idempotent for the saved MainMenu scene. The pre-existing `Assets/Resources.meta` GUID was restored to `93cbc34a3d1b475468ced14c22130024`; the generator now preserves existing folder and asset metas.

The root task reported 20/20 managed source/search tests passing; those tests were not rerun as part of this UI review.

## Still required for U07

- Seven affected assemblies passed warnings-as-errors source compilation against installed references. Native Editor compilation/import and the explicit disposable layout diagnostic remain NOT RUN.
- Use Unity to check prefabs for missing scripts/components and verify saved MainMenu and FirstContact references plus catalog/Build Settings parity.
- Run the test-only 100-interest/100-equipment fixture and profile scenarios for navigation, Apply/Cancel, NO/4x, loved/punishment, independent reward/punishment, clothing zero and more than two items, and Stop during a modal.
- Capture rendered 720p, 1080p and 1440p evidence (including 16:10), check clipping and keyboard interaction, and compare score formula and FirstContact story source/oracle.
- User manual Play and aesthetic approval remain pending.
