# Correction to the earlier four-task roadmap

The file `DazPoseWizard_Post_D8_Production_Roadmap.md` previously said each scene should select or compose explicit card libraries. **That was incorrect and is superseded.** Do not implement it, even if an older plan says otherwise.

**Actual user decision:** ONE global pool of selectable cards, with runtime tag matching. A newly authored global card whose tags/requirements match a draw automatically participates in older scenes. Scenes do not declare card libraries or lists and never need a content-file edit when the global pool grows.

Allowed: an authored script may explicitly execute a named card (`play Intro`, `Director.Execute(SomeCard)`), which is *not* a per-scene selectable library restriction. A project-wide automatic published-card discovery index may be generated for runtime/build support, but it is content-side infrastructure, not a scene membership declaration.

The named roadmap was not found in the reviewed checkout; it is historical context, not a missing execution prerequisite. This packet defines Task 1 independently. Tasks 2–4 are deliberately excluded as enumerated in START_HERE.md; do not infer additional work or authority from an unavailable roadmap.
