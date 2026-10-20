# U01 — Main Menu as actual product entry

**Depends:** F04. **Goal:** Normal user launches scenes from menu; authors can still direct-Play saved FirstContact in Unity.

Implementation: replace `MainMenuController` simple button list with data-bound SceneCatalog scene browser + details/Start. Ensure Editor Build Settings player order has `MainMenu.unity` first and catalog only published player scenes. Ensure correct `GameApplication` singleton bootstrap and `StartScene(id)` async navigation with error state and disabled repeat click, preserves profile/settings across scenes but cancels old session/effects via `StopSession`. Ensure first class `Return to Menu` UI path during/after scene, Stop remains reachable. No nonpublic Showcase or diagnostic test scenes in menu. Add minimal scene card fields art/description only if useful with backwards-compatible defaults. Avoid adding future hypothetical device prerequisites UI now.

Tests: invalid scene ID, missing path, double Start click, in-flight load, Return/Restart cancellation, no duplicate application root, catalog & Build Settings parity. EditMode state tests and inspect saved Menu/FirstContact scenes. The user manually tests actual navigation.

**Gate:** Opening MainMenu starts a real menu scene, Start opens saved FirstContact runner, return works without reentry or toy leak, public scene list only intended scenes.
