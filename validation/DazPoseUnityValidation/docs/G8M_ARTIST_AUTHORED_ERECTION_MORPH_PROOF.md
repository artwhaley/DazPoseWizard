# G8M artist-authored erection morph proof

**Status:** the user confirmed the erection control works. Generated endpoints and the subsequent weighted-shell checks pass numeric validation. The new bridge materials await visual review. Current character preparation and the hand-agent entry point are in [PARTNER_CHARACTER_HANDOFF.md](PARTNER_CHARACTER_HANDOFF.md).

## Open the acceptance scene

Open `Assets/Scenes/PartnerRigAcceptance.unity` in the Unity validation project and press Play. The existing smoke panel drives `Erection01`; use the 0, .25, .5, .75 and 1 buttons or the slider. The camera buttons cover front, side, 3/4, root and glans views. Body, graft, shell and eyelashes now use DUF-derived bridge materials with Lara's accepted skin glossiness correction.

For compliance, set `Erection01` to 1; the skinned shell can stay visible. The shaft1 and shaft4 controls provide ±3° and ±5° checks; the distributed bend is a separate experiment. The controls stay neutral outside the erect state. Restore returns to the erect base. The pelvis slider is a diagnostic that moves the main body/legs; leave it at zero for hand integration.

## Representation and numeric checks

`playererect.fbx` is the canonical body and skeleton; its shell is the canonical shell. Their source hashes still match the endpoint comparison. The generated body keeps the erect mesh, topology, UVs, material slots and weights. `ErectionToFlaccid` solves the full-weight target through the canonical skin matrices so the evaluated, skinned surface reaches the flaccid endpoint while the erect skeleton stays canonical. The shell source is unskinned, but the generated shell now copies corresponding anatomy weights and solves both authored endpoint surfaces through that skinning. Both channels use `Erection01 = 1 - blendShapeWeight / 100`.

Unity `SkinnedMeshRenderer.BakeMesh` comparisons against the endpoint FBXs report:

| Renderer | Endpoint | Maximum error | RMS error |
| --- | --- | ---: | ---: |
| Body | Erect, Erection01 = 1 | 0 mm | 0 mm |
| Body | Flaccid, Erection01 = 0 | 0.0000615 mm | 0.00000427 mm |
| Shell, after weight transfer | Erect, Erection01 = 1 | 0.00006665 mm | 0.00001321 mm |
| Shell, after weight transfer | Flaccid, Erection01 = 0 | 0.00006145 mm | 0.00001288 mm |

All checks are below the 0.01 mm tolerance. Body and shell topology, UV channels and material-slot mapping match between endpoints. Unity retains up to 10 influences per body vertex; 1,237 vertices have more than four. All 20,355 weighted vertices have normalized weight sums within 0.001 of 1. The importers use Custom weights, max 10, minimum 0.001, and constraints disabled. Runtime skin weights are Unlimited; the project-wide setting was not changed by this proof.

Unity 6000.5.9f1 clamps the importer minimum to 0.001. The source analysis found 2,402 of 44,228 positive source influence records below that cutoff (5.43%); this is recorded as a limitation, with no weight surgery.

## Geometry and rig diagnostics

The endpoint contact guides use 33 equal-arc stations, endpoint-local shaft-bone positions for station/tangent guidance, baked shaft-slot cross sections for centers and radius estimates, and a glans support-plane tip. The geometry is derived separately per endpoint; the guides do not interpolate the rejected local-bone translation data. Nominal radii are 2.061 cm flaccid and 2.325 cm erect.

The sampled contact-line lengths at Erection01 0, .25, .5, .75 and 1 are 19.46, 15.43, 14.75, 17.67 and 23.14 cm. The sample line remains continuous, but its estimated length shortens through the midpoint and then grows. Treat that measurement as a visual-review prompt, not an approval or a claim about surface attractiveness.

The canonical erect shaft pivots are closest to the erect geometry line (maximum error 5.43 mm). With the flaccid endpoint guide, mismatch reaches 150.23 mm at shaft7; this is the known fixed-erect-skeleton versus broad-morph mismatch. The report retains all seven bone distances at each sampled state in `TestOutput/G8MArtistErectionProof/UnityAcceptanceProof.json`.

The source shell remains unskinned; its generated copy is fully weighted and participates in erect-state compliance. Five sampled morph states preserve its authored transition, and visible-shell shaft1/shaft4 bend checks preserve a maximum surface gap of 0.208 mm. The controller disables shaft offsets outside the erect state. It captures local rotations once and applies offsets from that base every frame, so they do not accumulate.

## Evidence

- `TestOutput/G8MArtistErectionProof/UnityAcceptanceProof.json` — endpoint, influence, centerline, pivot and source-integrity results.
- `TestOutput/G8MArtistErectionProof/GeometryCenterlines.csv` — endpoint samples and nominal radii.
- `TestOutput/G8MArtistErectionProof/WeightThresholdImpact.json` — source threshold-impact analysis.
- `TestOutput/PartnerCharacter/Preparation.json` — current shell skin transfer, rendered endpoint/bend checks, DUF map validation and saved prefab references. This supersedes the original proof report's unskinned-shell status.

Source FBX hashes were checked before and after generation and remained unchanged. Numeric endpoint acceptance is separate from the pending human review of the intermediate morph, shell clearance and compliance appearance.
