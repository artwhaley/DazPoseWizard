# R04 — executable contact and wardrobe readiness

Depends R03. Allowed seams: PerformanceSceneBindings, IGripTarget.cs, PartnerContactTarget/PartnerBody validation, PerformerWardrobe validation helpers. No solver/calibration/morph support expansion.

1. Add `IGripTargetReadiness` beside IGripTarget: `bool IsReadyForContact(bool runtime, out string reason)`. Implement on PartnerContactTarget. Keep IGripTarget itself unchanged; legacy analytic targets remain valid and receive structural interface/reference checks.
2. Extract common target checks from TryEvaluate into the readiness method. Edit checks validate serialized body/profile/renderer/bone relationships, supported endpoint/compliance/scale and asset identities without binding caches, applying transforms, initializing gameplay or writing ValidationError. Runtime checks additionally require enabled/active body, live calibration and visible matching body/shell. TryEvaluate reuses these same checks before producing a frame.
3. PerformanceSceneBindings validates declared Contact through readiness when supported. Preserve same-actor hand profile checks. Return owner/property-path/reason issues; no target evaluation side effects during editor preflight.
4. Add a read-only PerformerWardrobe readiness helper: call existing catalog validation and TryValidateConfigurationBinding; reject NeedsCompile candidates outside explicit setup preview; verify declared preset resolution, character signature, reachable fit validity and body/profile/surface ownership. Runtime checks report initialized BindingDiagnostic. Production playback requires released catalog; setup explicitly opts into compiled candidate preview.
5. Add serialized `WardrobePreset[] wardrobePresets` to declarations, default empty; validate only declared assets and required capability. Do not require every catalog preset for a sequence using one outfit. Preserve older scenes with empty declaration arrays.
6. Never clear/rebuild the wardrobe queue or call Grip during preflight. Missing optional contact/wardrobe does not block speech-only declarations.

Run `-Ticket R04`: valid ready production actor; missing target body; disabled body; changed profile mesh; unsupported endpoint/compliance/scale; released config mismatch; stale candidate; invalid declared fit; speech-only without optional systems. Assert exact offending owner and nonempty property/reason. Snapshot relevant transforms/assets before/after edit validation to prove no mutation. Live target removal after Begin must still fail safely at execution; a prior preflight pass is not a permanent certificate.

Gate: valid declared production subset passes; each invalid case fails before command launch. Do not strengthen rigidity restrictions or shrink supported contact span.
