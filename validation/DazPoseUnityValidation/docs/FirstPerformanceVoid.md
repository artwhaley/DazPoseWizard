# P0.C — First Performance Void

Baseline: `dd13db4002e257294ffc1a7efc6c7721d52e9b54` (includes P0.B1).

## Open and preserve tuning

Open `Assets/Scenes/FirstPerformanceVoid.unity`, or use **Tools > DAZ Pose > First Performance Void > Build or Open Lounge**. Once the scene exists, the menu only opens it. It does not reconstruct geometry or overwrite chair/anchor/performer/fog placements. The separate **Refresh Generated Materials and Volume Defaults** menu deliberately resets only the three owned materials and global Volume profile. Save your own placement edits normally.

The initial scene was constructed with Unity 6000.5.9f1/HDRP 17.5.0 in an isolated staging project. The working source was Unity's current scene backup `Temp/__Backupscenes/0.backup` from October 1, 2026, 11:37:49 AM: the saved PoseValidation scene did not yet contain the working chair and locomotion references. The snapshot is consumed only during construction; the finished scene does not depend on the temporary snapshot asset. No existing validation scene is saved or replaced by this process.

The complete source performer hierarchy is cloned, preserving its Generic Genesis skeleton, pose/expression/breathing/blink/gaze/SALSA/speech components and asset references. The source chair hierarchy is copied into an owned prefab with its authored geometry, scales, rotation and local approach/seat anchors. Only the complete chair's world placement changes; its scene renderers receive the dark accent material. Scene-specific smoke harness targets are rebound to this room's camera, chair and walk marker. This does not recreate the chair from guessed dimensions.

## Assets and hierarchy

All created Unity assets have companion `.meta` files. No package, vendor animation, canonical Lara importer/material, or existing validation scene changes are required.

| File under Assets | Purpose |
| --- | --- |
| Scenes/FirstPerformanceVoid.unity | Permanent editable native scene |
| FirstPerformanceVoid/FirstPerformanceVoidControls.cs | Three direct room commands through existing performer APIs |
| FirstPerformanceVoid/Editor/FirstPerformanceVoidBuilder.cs | Focused initial native construction and explicit look refresh |
| FirstPerformanceVoid/Materials/M_VoidGlossBlack.mat | Floor/stage glossy black HDRP Lit |
| FirstPerformanceVoid/Materials/M_NeonMagenta.mat | Perimeter/stage emission HDRP Lit |
| FirstPerformanceVoid/Materials/M_DarkAccent.mat | Lounge material |
| FirstPerformanceVoid/Settings/V_FirstPerformanceVoid.asset | Scene global Volume profile and persistent component subassets |
| FirstPerformanceVoid/Settings/ConstructionRecord.txt | Source chair/anchor construction provenance |
| FirstPerformanceVoid/Textures/T_FogDensity32.asset | Shared native Texture3D |
| FirstPerformanceVoid/Meshes/StageGlowRing.asset | Compact generated stage rim mesh |
| FirstPerformanceVoid/Prefabs/ValidatedLoungeSeat.prefab | Authored working chair hierarchy |

Scene root `FirstPerformanceVoid` contains `Environment` (Floor, Platform, Lounge, Lights, Volumetrics), `Performer`, `PerformanceMarkers`, `MainCamera`, and `SceneVolumes`. The original performer smoke panel remains; a small room panel adds **Walk across floor**, **Walk near platform**, and **Look at camera**.

Floor: 18 × 14 m, thin cube, top at Y=0; four magenta strips around its perimeter. Platform: 4 m diameter, 0.35 m high, center (-3,0,2), glowing rim. Lounge root: (4.5, original authored Y, 2), preserved source rotation. Walk markers (5.5, actor Y,-3.5) and (-3, actor Y,-1.1) stay on the floor and outside the stage. The raised platform is scenery: its manual composition marker is for editor placement, not WalkTo. No vertical navigation has been added.

## HDRP look defaults

| Feature | Serialized configuration |
| --- | --- |
| Gloss black | Base RGB .012/.014/.021, smoothness .84, metallic .08 |
| Dark accent | Base RGB .027/.023/.037, smoothness .55, metallic .03 |
| Magenta neon | HDRP Lit native emission, RGB 1/.015/.28, 3500 nits; HDMaterial APIs validate keywords |
| Bloom | Intensity .35, scatter .5, threshold 1, dirt intensity 0 |
| Exposure | Fixed 7 EV100, compensation 0; no automatic exposure adaptation |
| Void | Black flat ambient, no sky, almost-black camera clear color |
| Global fog | Constant dark color, volumetrics enabled, mean free path 120 m, height -.2 to 1.8 m, depth extent 40 m, maximum distance 60 m, anisotropy .2 |
| Camera | FOV 58°, near .08 m/far 70 m; scene camera overrides enable HDRP fog, volumetrics, reprojection, postprocess, Bloom and exposure |
| Reflections | No additional reflection system/probe/SSR feature; initial floor sheen uses direct light specular |

Three realtime spotlights: soft neutral key (6500 lumens, sole shadow caster at 512 resolution), magenta platform accent (4200 lumens), magenta lounge accent (2600 lumens). All use native Unity 6 light units; volumetric dimmer .45. No baked lighting is needed.

| Local fog bank | Center (m) | Size (m) | Mean free path |
| --- | --- | --- | --- |
| Left | (-6,.35,0) | (4,.85,5) | 8 m |
| Rear | (0,.5,5) | (7,1.2,3.2) | 12 m |
| Right | (7,.28,-.5) | (3.5,.65,4) | 10 m |
| Lounge | (4.5,.25,4.4) | (4,.6,2.2) | 16 m |

Four HDRP LocalVolumetricFog components share one 32³ RGBA32 Texture3D (128 KiB before serialization overhead). Deterministic periodic smooth value noise at three frequencies produces the density mask, with density in all channels including alpha. Repeat wrapping/trilinear filtering, differing tilings/rotations, feathered edges, and slow built-in horizontal texture scrolling provide variation without a custom runtime fog script. Disable the `Volumetrics` group to remove local banks, or increase individual mean free paths to reduce density. Global haze remains separately controllable in the Volume profile.

Main performance costs are HDRP volumetrics/Bloom, three realtime lights, one shadow caster, and Lara's existing rendering/performer stack. No additional reflection pass, third-party dependency, URP asset, downloaded VFX or vendor package update was introduced.

## Acceptance status

Native generation serializes/configures assets and preserves source references. It is not a rendered visual review or Play Mode acceptance. WalkTo/SitAt/StandUp, gaze/expression/blink/breathing, Speech/SALSA, chair fit, fog appearance, exposure and reflections must be reviewed manually by Art. The room layout provides clear floor-level destination paths; actual movement completion is not asserted before that review.

## ART'S FIRST PERFORMANCE VOID REVIEW

1. Open `Assets/Scenes/FirstPerformanceVoid.unity`. Save any current unsaved scene before switching. Let Unity import the new assets, then enter Play mode.
2. Use the existing performer panel to change poses and expressions. Observe autonomous blink and breathing.
3. Click **Walk across floor** in the room panel. Observe movement and exact arrival. Click **Walk near platform** and confirm she stays on the floor.
4. In the existing panel's **P0.B Anchored Seating** section, click **Sit Basic** or **Sit CrossLegs**. Confirm the approach, sit motion and authored seat placement.
5. While seated, click **Say A**, **Say B** or **Say C** under Queued Speech. Listen and observe SALSA articulation. Apply **Expression A/B/C**, then **Clear**; observe expression/speech ownership.
6. Click **Look at camera** in the room panel; observe gaze while seated and speaking.
7. Click **Stand Up** in Anchored Seating. Confirm responsive uncrossing if applicable, correct stand motion, then walk across the floor again.
8. Exit Play mode before permanent tuning. Move the complete lounge chair root to reposition it; use its existing Seating Preview to tune anchors, preserving their local relationship. Save the scene. Rerunning Build or Open must preserve that tuning.

Subjective questions (all retained):

1. Is the room dark enough without making Lara disappear?
2. Is the magenta neon the right hue/intensity?
3. Is Bloom elegant or too smeary?
4. Does the floor feel expensive/sleek enough for a placeholder?
5. Does the stage feel like an intentional focal point?
6. Is the lounge positioned well relative to the stage?
7. Does the low fog actually read as billowy/pooling rather than uniform haze?
8. Is the fog too dense around Lara?
9. Does Lara look good under the current lighting?
10. Does the environment finally give the character performance enough vibe to judge a first directed scene?
11. What single environmental element bothers you most?
12. Is this good enough to STOP environmental work and return to character performance/directing?
