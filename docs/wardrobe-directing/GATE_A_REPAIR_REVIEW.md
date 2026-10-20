# Gate A delivery repair and complete path review

Source revision: `13492a7`, on `main`. Gate A remains **WAITING_ON_ART**. No Unity process, native test, Play, player, build or hardware operation was run by the executor.

## Reported error and saved inputs

The candidate folder contains a valid **published** catalog, generation `d0b34840249bc9e812337938d1214c5f5cfb79f958dbd764afe2b6bca12cce0a`, with a matching configuration. `WardrobeGenerationPublisher.PublishCandidate` intentionally advances that catalog in place, marks it released, clears its authoring link, and replaces its presets with immutable snapshots. Candidate recovery incorrectly rejected every released catalog. Thus repeating preparation after successful publication produced the misleading incomplete-folder error.

Recovery now recognizes both the editable candidate and its published result. It checks catalog validity, configuration/generation compatibility, all assignment keys/layers against the corrected source, preset membership, package source fingerprints and hair policy. It prefers a matching published result. Incomplete or incompatible diagnostic folders are retained and a new unique candidate path is used when needed.

## Findings and changes

| Area reviewed | Defect found | Correction |
| --- | --- | --- |
| Candidate lifecycle | Published catalog rejected as an invalid draft | Recover published and editable states; reuse the published generation without republishing. |
| Partial output recovery | Existing candidate or scene permanently blocked retries | Preserve incomplete output; create a versioned diagnostic replacement; reuse completed compatible output. |
| Window reload | Corrected staged assets were replaced by production defaults after losing transient selection | Discover compatible saved candidates/releases and recover the matching rig manifest on enable. |
| Workflow | Several independent buttons allowed repeating the wrong stage | One **Prepare / resume Gate A** operation executes the ordered stages and skips completed publication. Reports retain the failed stage. Individual actions remain available for diagnostics. |
| Canonical renderer | Body found through a blendshape heuristic; hierarchy owner lookup failed for inactive prefab assets | Use production `PerformerWardrobe.Body` directly. Validate explicit owner/body reference and ancestry, including runtime binding. |
| Rig generation | Mesh/material/bind-pose checks could fail after earlier prefabs had been written; reuse trusted only a hash | Validate all planned inputs first, validate reused descriptors, and check any existing manifest before output creation. Manifest validation rejects unknown presets and layer mismatches. |
| Scene persistence | Direct prefab-instance changes lacked explicit override recording | Record actor/wardrobe overrides, save the diagnostic composition, reopen its saved file and validate actor/catalog/rig/template references. |
| Actor visibility | Copied First Contact actor starts hidden and its normal reveal sequence is disabled | Configure the diagnostic instance to start visible while preserving pose/expression settings. |
| Attachment coordinates | Generated rigs could be attached in the actor-root frame instead of the canonical body frame | Bind instances in the body's frame and place the diagnostic clothing/hair roots under that body. |
| Cached selection rendering | Retired hair/clothing renderers could stay enabled after leaving the active binder inventory | Explicitly hide removed pieces while retaining their cached resources for reuse. |
| Rollback lifetime | Switching cache active keys could make the prior resources eligible for eviction during rollback | Pin the previous clothing/hair leases across the atomic commit. |
| Add-layer assertion | Runner searched only up to the current ceiling, so it falsely failed Add from naked | Use the production `LowestHidden` layer rule. |
| Visible garment labels | `LayerPieceIds` contains every populated layer, even hidden ones | Filter by visible mask; independently inspect actual enabled descriptor renderers and compare semantic garment IDs, hair key, fit mesh and bone ownership. |
| Second actor | Cloning the live first actor also copied its cached native attachments/local bones | Save a clean inactive template before Play; instantiate it with remapped owner/body/root references. Keep the second actor visible for inspection, then verify actor/attachment/skeleton disposal after **Remove second actor**. |
| Hair inventory | Fixture demanded a second hairstyle that does not exist | Use released keys. Current release has only Emiko; toggle Emiko/none, verify clothing preserves both selections, and exercise distinct selections on two owners. Record swapping two different hair assets as NOT_RUN. |
| Report completion | Any single successful assertion could produce a passed report | Require the complete remove/add/no-op, hair-retention, independent-owner and disposal cases. Capture actual rendered pieces, fit/body refs, bone instance identities and attachment counts. |
| Managed check delivery | Wardrobe test `.csproj` was ignored and absent from Git | Track the hand-authored project using an explicit ignore exception. |

## Checks actually performed

- `python scripts/check-unity-source.py`: all seven source assemblies passed against installed reference inputs. This invokes Roslyn, not Unity.
- Pure managed tests: **25 wardrobe, 90 story, 93 rehearsal passed**. The rehearsal project retains its existing CS0649 warning.
- Seven new workflow/recovery cases cover published recovery after its authoring link is cleared, mismatched configuration rejection, first delivery order, repeated resumes, failures after publication, and stopping before downstream mutations on failed preparation/publication.
- Three new data checks use a captured metadata fixture from Art's actual published release. The production `WardrobeBoneBindingPlan` accepts all **24 pieces** against the **173 canonical bones**, including their actual local-bone facts.
- Saved fit metadata has seven validated fits. First-outfit masks **0 / 2 / 6** contain **none / panties / panties + dress** respectively and reference three distinct prepared body meshes. This checks saved declarations and identities, not visual fit quality.
- The released inventory has exactly one hair key: `first-outfit/EmikoHair_59875`.
- Metadata inventory: **4,732 GUIDs, no duplicates**.
- W00 protected comparison: 41 of 42 files match. The sole mismatch is the already observed Unity serialization of `rigBinding: {fileID: 0}` in production Lara.prefab. That existing change was preserved and excluded from implementation commits. No production appearance reference was advanced.
- The existing Art-generated release/candidate assets were preserved. Evidence includes fingerprints of both generated immutable release trees; they were not regenerated by the executor.

Evidence: [managed outputs](evidence/gate-a-managed-checks.json), [saved-asset audit](evidence/gate-a-delivery-audit.json), [source/check index](evidence/checks.json), and [captured bone/fit metadata](../../tests/DazPose.Wardrobe.Tests/Fixtures/GateAReleaseInputs.json).

## Native retry and remaining limits

1. Wait for source compilation in the already-open Unity project.
2. Open **DazPose > Wardrobe > Appearance Delivery** and click **Prepare / resume Gate A** once. Do not delete folders or restart publication manually.
3. Wait for **Gate A setup complete**, then click **Open Gate A scene** and enter Play yourself.
4. Follow Gate A's updated control sequence in [MANUAL_GATE_PROTOCOL](../../newtickets/DazPoseWizard_Wardrobe_Protected_Cards_Execution_Packet/MANUAL_GATE_PROTOCOL.md). The sole-style hair control is labelled **Toggle Emiko / no hair**. Use **Remove second actor** after inspecting the independent actor, then **Write report**.

Unity AssetDatabase/prefab execution, saved-scene reopening under the actual Editor, runtime mesh/effect projection, animation and visual fitting still require Art's native Gate A run. Source compilation and managed data checks cannot certify those. Distinct-style hair swapping cannot be observed from the current inventory and is explicitly not claimed. W04 remains gated on Art's bounded Gate A acceptance.
