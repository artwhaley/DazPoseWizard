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

## Corrections after Art's first review

Art accepted Bloom, floor, stage and initial lounge placement, but found Lara too dark, the neon red, and the fog invisible. These lighting corrections preserve those accepted settings and the camera placement:

- Magenta emission and accent lights now use RGB 1/.015/1: equal red and blue, rather than the original blue .28 (which becomes substantially weaker after linear conversion). Emission stays 3500 nits and Bloom/exposure stay unchanged.
- Two soft, unshadowed point lights are children of the performer root, so they follow locomotion and seating automatically. The warm key uses 1600 candela and the cool fill 950 candela. Only the cloned Lara renderers receive rendering layer bit 2, while keeping their original world-lighting mask. These two lights use only bit 2 and have volumetric dimmer zero: no floor or fog lighting.
- The existing active HDRP asset now supports Light Layers; the lounge camera enables them. This capability is required for the character-only lights. Existing camera defaults and unrelated scene renderer masks are preserved.
- Local banks use mean free paths of 2.5 m (Left/Rear/Right) and 4 m (Lounge), with albedo .85. Existing density texture, feathering, scrolling, placement and size remain. Two magenta point lights at (-4.5,.65,2) and (5.5,.55,2.5), 1800 candela/range 12 m, illuminate only volumetrics: diffuse/specular contributions are disabled. Existing magenta accents now have volumetric dimmer 1.
- There are now seven realtime lights total; only the original neutral key casts shadows. The additional cost is four unshadowed point lights, of which two illuminate only fog.

The correction is already serialized into the scene and material; no setup command is needed. The optional **Tools > DAZ Pose > First Performance Void > Apply Lara Lighting and Fog Corrections** command reapplies these defaults without replacing objects or moving camera/chair/fog banks. Fresh initial construction also applies them. Visual confirmation remains Art's Play Mode review.

### Loaded-scene correction delivery

The first correction was copied into the scene on disk while an older version was already loaded in the editor. A subsequent save of the loaded scene replaced the new hierarchy; the project scene then contained none of the four added lights. This was a delivery failure, not visual acceptance of the corrected setup.

Lighting revision 1 is now stored in the room controls. An editor-only, one-time upgrade applies the correction to an already loaded FirstPerformanceVoid scene after script reload, on scene opening, or immediately before Play. It preserves current camera/placement edits and saves that corrected scene. It does not create lights at runtime or repeatedly retune a scene already upgraded. If scripts were imported during Play, the upgrade waits until Edit mode. The latest generated scene preserves Art's moved camera at (4.92,2.19,-5.09).

### Revision 2 — illuminated perimeter and softer skin

After revision 1, Art confirmed a substantial lighting improvement but requested stronger fog enclosing all four sides and less glossy skin. Revision 2 uses the same one-time editor delivery mechanism and preserves current camera edits.

- Four banks bound the floor: Left/Right centers (-8,.6,0)/(8,.6,0), sizes (2.4,1.6,14); Front/Rear centers (0,.6,-6)/(0,.6,6), sizes (18,1.6,2.4). The old Lounge bank becomes Front. Mean free path is .65 m, albedo .95, with feathered upper edges and horizontally tiled/scrolled existing 32³ noise. The middle of the room remains outside these banks.
- Four unshadowed magenta fog-only point lights, one per edge, use 4500 candela and 12 m range. They do not light floor or skin surfaces. Total realtime light count becomes nine, still with only one original shadow caster. Fog brightness/density does not increase its resolution; broader bank coverage and two additional lights increase the work within the existing volumetric pass.
- Skin materials used by this scene are copied to `Assets/FirstPerformanceVoid/Materials/LaraSkin/M_LaraSkin_<surface>.mat` for Face, Ears, Torso, Arms, Legs, Lips and EyeSocket when those surfaces are present. Imported materials and shader graphs are unchanged; eyes, nails, teeth and hair are unaffected. All texture maps remain. The copies use roughness .72, lobe roughness .72/.6, glossy weight .5, dual-lobe weight .2, top-coat weight .03. This is a quick specular adjustment, not a new skin shader or diffusion-profile overhaul.
- No screen-space/ray-traced GI was enabled. Emissive surface bounce would require a GI solution; fog illumination remains native light components. An FPS/millisecond estimate requires profiling the target GPU and resolution and is not asserted here. Visual acceptance and playback remain manual.

### Revision 3 — rising smoke, replacing the fog hedge

Art rejected the rectangular upper boundary of revision 2. The local `Environment/Volumetrics` banks are now inactive, retained only as an editable fallback reference. Four native ParticleSystems under `Environment/Perimeter Rising Smoke` emit along the floor perimeter at X ±8.6/Z ±6.6. Smoke uses world-space simulation, randomized 6–10 second lifetimes and 0.2–0.4 m/s upward drift, gentle noise and lateral drift, random rotation, size growth, and smooth per-particle birth/death opacity. There is no shared horizontal termination plane. Each emitter is prewarmed and loops automatically in Play mode; no runtime setup fallback/script is required.

The project-owned `Shaders/PerimeterSmokeLit.shadergraph` is copied from Unity's installed HDRP 17.5.0 `ParticleLitSoft` sample (Unity Companion License notice included). Its two subgraphs are existing installed Shader Graph dependencies, not additional packages. Native lit transparent billboards use particle vertex color, texture alpha and scene-depth soft intersections. A generated 128² RGBA32 mipmapped `Textures/T_SmokeBillow.asset` combines seven soft density lobes with three frequencies of noise and zero opacity at the texture edge. `Materials/M_PerimeterSmoke.mat` binds that texture and a .5 m soft-intersection fade.

Four `Smoke Edge Left/Right/Front/Back` lights replace the four fog-only lights. Rendering layer bit 4 restricts them to smoke particle renderers; they do not affect floor/Lara, cast no shadows, and have volumetric contribution zero. They use magenta 4500 candela/range 12 m. Total light count remains nine. Faint global fog and existing Lara lighting/skin/Bloom/exposure are preserved. Typical steady-state count is approximately 400 billboards (50 emissions/second × 8-second average lifetime), bounded by 720 total. The main extra rendering cost is transparent overdraw; no GI, fluid simulation or third-party asset was added.

Lighting revision 3 upgrades an already loaded lounge once in Edit mode, retaining camera and placement edits. Stop Play and allow script/shader import before evaluating it. Manual visual checks: billows should rise, expand, drift and gradually disappear at varied heights; no box-shaped ceiling should remain; intersections near the floor should be soft; all four edges should show smoke while the room's center stays open. Particle appearance, light response and runtime performance have not been accepted by the executor.

### Revision 4 — continuous coverage, Game-view visibility and camera preservation

The current saved scene was reread before this correction. Its camera position was (2.42,1.34,-.14), with rotation quaternion (.027425291,-.93873113,-.0018371617,.34355253). The scene already serialized Play On Awake, prewarm, enabled box shapes spanning the edges, and enabled camera transparency/Light Layers. Therefore this was not treated as a missing Play On Awake setting or an authored point-shaped emitter.

Emission increases from 11/14 particles per second to 42/54 for 14/18 m edges: three billows per metre per second, approximately 1536 particles at the average lifetime, with a 650-particle limit per edge. Box position/rotation are explicitly zeroed and the full line dimensions retained. Peak opacity increases to .5. The lit shader adds constant magenta emission (180,18,150) so visibility is no longer exclusively dependent on a light reaching the billboard; texture alpha and lifetime fades still define each billow's soft shape. This is a deliberate glow approximation for the neon environment, not physical emissive bounce lighting.

Only existing smoke ParticleSystem/Renderer/Transform blocks and the lighting revision were transferred from native generation into the freshly reread scene. All camera component/transform blocks were compared before writing and preserved exactly. No whole-scene replacement was performed. Later loaded-scene smoke upgrades change only smoke, not the accepted character lighting or skin. The existing room panel now reports particle count and running emitter count to distinguish simulation from rendering failures during manual Play Mode review. Expected steady state is four running emitters with nonzero particles. Visual acceptance remains manual.

The runtime particle-count reference exposed a missing prerequisite: the project had not enabled `com.unity.modules.particlesystem`. The installed Unity built-in module `1.0.0` is now explicitly enabled in the package manifest/lock, with no dependencies or third-party/HDRP package updates. Editor-only native generation did not establish that the runtime assembly was available. No camera or scene data changes accompany this dependency fix.

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
