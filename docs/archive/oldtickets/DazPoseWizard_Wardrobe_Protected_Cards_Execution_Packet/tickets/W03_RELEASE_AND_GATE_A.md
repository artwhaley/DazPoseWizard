# W03 — Correct first-outfit release and Gate A

Depends W02 source-ready. Existing WardrobeCandidateSnapshot/Configuration/FitCompiler/GenerationPublisher are authoritative; read their actual current implementations and wardrobe import handoff for the relevant operation only.

## Files

Extend Editor/Wardrobe delivery services to clone current released inputs into owned candidates, compile/publish through existing services, generate the reusable rig and compose Gate A. Update publisher only where needed to preserve unaffected released configurations; do not invent another fit compiler.

Art-generated new candidates: Assets/__WardrobeProtectedCards/Candidates/. New releases use existing publisher's unique roots; reusable derivatives use Assets/Production/Wardrobe/Rigs/<generation>/. Production LaraCatalog/LaraConfiguration and Lara.prefab remain at old references through A–C.

## Steps

1. Prepare a candidate from the production released generation/configuration, not the unrelated default draft catalog. Clone writable configuration/preset inputs; retain all three catalog entries. Change only first-outfit layer mapping: dress=2, panties=1.
2. Determine all reachable fits using current compiler. Full/naked unaffected fits may reuse compatible released data only if dependency hashes and actual geometry meaning match. The new panties-only intermediate needs correct prepared fit; never relabel dress-only.
3. Invoke existing publication via Art's window button, with transaction/preflight rollback and old generation retained. No overwrite of immutable generation paths. Stage the new matching catalog/config as diagnostic assets, not production reference advance.
4. Record publication report with old/new IDs, exact changed inputs, reachable masks and garment sets, fit hashes, derived prefab paths and GUIDs. The reusable rig references the resulting released data.
5. Deliver exact Gate A steps and wait. Art generates assets/compiles/opens scene/enters Play. Read resulting report; compare protected inputs against W00. Native source methods/builders are not to be executed from shell.

## Exit

Source checks plus Gate A PASS required. Mark W02 and W03 COMPLETE only after correct native prefab/fit/layer/independent-hair behavior is observed and actual asset refs reported. Missing fit: WAITING_ON_ART/BLOCKED with exact combination and failed publisher diagnostics; no fallback naked fit, no extra user Daz import unless evidenced necessary. On FAIL repair same gate.

Stop before W04 while Gate A is unpassed. No production appearance advance or FirstContact startup claim yet.
