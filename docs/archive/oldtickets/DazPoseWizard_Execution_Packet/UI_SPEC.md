# Phase 2 detailed player UX / production UI contract

## Navigation flow

Normal player path `MainMenu → Scene browser → Scene detail → Start → saved gameplay scene → Stop/Return → MainMenu`. The existing `SceneCatalog` is sole published scene list, **not** a card registry. Respect stable scene IDs/paths; catalog entries may receive optional art/summary fields compatibly, no renumbering. At current baseline only FirstContact is public; other examples/validation scenes remain out of launch/browser/build. `GameApplication` remains DontDestroyOnLoad and session owner; one active session; stop/cancel/tear down UI and async work before navigating; guard double clicks/transitions; explicit load failure should leave usable menu and present diagnostic, not silently swallow.

Add game-side first-scene/bootstrap verification without breaking Unity author quick-start: built player's first scene is MainMenu; direct Editor Play of FirstContact for author manual testing still works. Menu should show one quality card with title, description, optional illustration, status and Start; supports hundreds of future entries through scroll/grid. A scene with unfinished content isn't published by enabling it accidentally: validate saved scene runner/source and required binding readiness in publisher/editor, not runtime arbitrary story card lists. Never invent hardware prerequisite policy here—that will be designed in future device-focused scene.

## Visual language

Current Void aesthetic is dark, upscale, restrained glowing accent, not a dashboard of debug labels. UI: high legibility, compact geometry, coherent dark backgrounds, limited luminous accent, large main headings but restrained body text, consistent spacing, radio/segmented choices instead of cycling labels, explicit selected states; hover, focus, disabled and error states. Implement actual Unity uGUI prefabs with layout constraints and reliable raycasts/event system. No hardcoded screen-position pixel rectangles, no repeated instance-specific `StoryUi.Label` creation except simple dynamic content. UI skin/theme settings centralized in one serializable prefab or palette asset. Verify at 1280×720, 1920×1080, 2560×1440 and 16:9/16:10; CanvasScaler sensible limits; keep Stop reachable above blocking prompts.

## Main Menu

Header/logotype and concise subtitle, featured scene panel or grid (from SceneCatalog), Settings and Quit. Card state shows title/description; missing scene indicates unavailable with reason instead of blank click. Opening Settings doesn't spawn a second top-level Canvas with inconsistent scaling. Return to menu after normal completion is user-action or end-screen (not instantly auto-exit on last dialogue); session Stop always available. Escape/back behavior defined: close modal, then Settings, then optional quit confirmation; never navigate through unsaved edit without discard confirmation.

## Interest editor at 100 entries

- Search textbox matches stable ID, display name and description; case-insensitive, trimmed, debounced only if needed.
- Category groups derived from existing `themeTags` (preferred display category from first theme tag) with "Other" fallback; optional author-friendly explicit display group field can be added to `KinkDefinition` without changing profile IDs. Group filters: All, Enabled, Disabled, relevant categories. Search + filter combine and preserve draft choices.
- Each row compact (typical ~52–76 px at 1080p depending on viewport), name with optional tooltip/details, explicit 4-way **NO / 1x / 2x / 4x**, explicit Loved/Neutral/Punishment as a compact selector. Sentiment can be visible only when not NO, but stored value not erased when NO. No cycling single button.
- Keyboard arrows/tab enter/space operate; focus visible. Header may show count Enabled / total and explain frequency/sentiment. No manipulation of submission formula; that is a separate original computation.
- Initial defaults and out-of-catalog user data survive; Apply once; Cancel none. Dialog reopens at initial scroll/filter if session warrants but does not leak values between players.

## Equipment editor at 100 entries

- Search name/ID/tags, group by identity/theme tags or optional display category, filters All, Owned & Willing, Not Owned, Reward, Punishment; no modal nested per item for basic choices.
- Compact row shows name and 3 explicit controls: **Owned / willing**, **Reward allowed**, **Punishment allowed**. The last two are independent, not a single mode; preserve their values even when Owned is disabled unless existing data contract explicitly says otherwise. Selection dialog never auto-equips/prepares anything.
- Optional details section shows tags, compatibility without needing 220px per row. Compatibility/ownership/selected-session equipment semantics remain in `EquipmentCompatibility`/session setup, not duplicated in view.
- 100 entries, no visual overflow, stable search/filter/scroll, draft edits retained while filtering and sorted order deterministic.

## Clothing dialog

Existing six binary slots (shoes, socks, shirt, pants, bra, underwear), compact responsive layout. No persistent profile edits. Confirm writes `World` once; Cancel leaves world state unchanged. In First Contact `Popup.RemoveClothingItem` and `Popup.Ready` remain blocking when authored; global UI facelift must not add extra dialogue or change sequencing.

## Cross-cutting modal behavior

- Explicit header, scrollable body, fixed footer Apply / Cancel; unsaved changes prompt on navigation outside form; Esc cancels or closes as appropriate; pointer/controller focus returns to launching button. Stop/phase cancel must release awaiting promise *without* commit and destroy/or hide overlay. Double click Apply cannot commit twice.
- No simultaneous blocking prompts; queued behavior may remain error if existing story contract requires exclusivity. Dialog content shouldn't occupy/occlude Stop control. Safe `Canvas.sortingOrder` and `EventSystem` uniqueness per scene; don't create one global persistent modal Canvas every time Settings opens.
- Reusable presenters can render on Menu Settings and in-game story prompts without copying Kink/Equipment logic into `SettingsMenuController` and `StoryPromptService`. This is the core acceptance.

## Diagnostics and manual test

Screenshots at 720p and 1080p, populated synthetic 100-item catalogs, search no-results state, keyboard focus, settings vs in-session value parity, explicit Cancel/reopen, Apply/reload, 0/2/6 clothing branches, and FirstContact stop during modal. Source-driven UI tests can cover state and layout bounds in EditMode. User evaluates final aesthetic polish during manual Play.
