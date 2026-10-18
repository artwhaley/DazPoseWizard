# U06 — Integrate production UI through actual scenes

**Depends:** U04/U05. **Goal:** Replace prototype prompt screens without changing authored FirstContact sequencing.

Wire `SettingsMenuController`/`StorySettingsProfileController`, `StoryPromptService.Editors` and saved FirstContact prompts to shared U02 screens. Use one coherent menu/sessions modal host with correct Canvas raycast/order, focus ownership and cancellation. `Popup.Show(Kinks)` and `Popup.Show(Equipment)` still await Apply. `Popup.Show(Clothing)` still commits only transient world slots, and Ready/removal messages remain simple, readable, non-overflowing. Do not change 19 Lara transcripts, dialogue timing, mood, score or engine API. Esc cancels user edit; Stop removes overlay and resolves awaiting task cancellation; main menu Return closes prompt safely. Existing `UI.ShowAction(ICame)` nonmodal control remains visible and clickable when appropriate; do not cover it with persistent stray modal elements.

Tests: actual saved scene bindings and prefab refs, no duplicate EventSystem on menu→scene→menu, Stop while modal open, Apply→snapshot refresh, Cancel→no refresh, early action after long prompt, multiple reentry.

**Gate:** MainMenu Settings and FirstContact prompts both use same polished presenters, no prototype 120px/220px rows remain as active UI.
