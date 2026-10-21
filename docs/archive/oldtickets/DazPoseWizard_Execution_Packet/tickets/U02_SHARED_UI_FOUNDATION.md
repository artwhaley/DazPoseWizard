# U02 — Styled reusable uGUI screen infrastructure

**Depends:** U01. **Goal:** One production UI composition system reused by Settings and session prompts.

Under `Assets/Production/UI/` introduce prefabs for modal/screen shell, fixed header/footer, search toolbar, scroll viewport, row templates, buttons/segmented choices/checkbox, scene card and shared palette (respect existing dark upscale Void look). Render via uGUI with TextMeshPro, central CanvasScaler, consistent focus/keyboard behavior, no overlapping EventSystems. Replace oversized imperative `StoryUi` layout factories for full screens with reusable presenter/view components; keep StoryPromptService's awaited API as adapter and ability to reserve accessible Stop strip. Add pooled/recycled row list or verified performant 100-row implementation; prefer data-driven filtering without rebuilding all entries every keystroke. Responsive 1280x720 min, 1920x1080, 2560x1440 and 16:10; fixed footer independent of scrolled content.

Tests: prefabs exist and scene wiring references them; layout bounds tests at 3 resolutions, modal stacking, no clipped footer or intercept of session Stop, keyboard traversal, search focus, duplicate canvas prevention. No changes to profile persistence.

**Gate:** Both Settings and story adapter can host same empty/filled reusable view without duplicating UI code. Screens are not 50 large generated generic columns.
