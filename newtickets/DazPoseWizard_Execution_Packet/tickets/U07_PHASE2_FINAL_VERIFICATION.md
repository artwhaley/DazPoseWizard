# U07 — Phase 2 integrated readiness

**Depends:** U06. **Goal:** certify exact delivered Unity scene assets and UI data flow; do not claim human visual Play acceptance.

Run dotnet suites, UI EditMode tests, source assembly warnings-as-errors, prefab missing-script checks, saved `MainMenu` and `FirstContact` serialized refs, public scene catalog/BuildSettings parity; mock 100 interests + 100 equipment via test-only fixture without polluting user catalogs. Capture screenshot or Editor preview evidence at 720p/1080p/1440p; verify no list/header/footer clipping. Validate one profile scenario with navigation, Apply/Cancel, No/4x frequency, liked/punishment, equipment reward+punishment independent, clothing 0 and >2, Stop. Compare score formula and FirstContact source/oracle unchanged; check no direct unapproved menu Play/build.

Document UI changes, hotkeys, screens and manual user inspection under `docs/p2-p3/PHASE2_REPORT.md` and update progress.

**Gate:** Technical Phase 2 PASS on real saved scene references. User manual UI aesthetic approval remains PENDING.
