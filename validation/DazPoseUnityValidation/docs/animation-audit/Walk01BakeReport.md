# KAWAII Walk01 canonical Lara bake

Source clips are sampled through the configured Human Avatar on `laraHumanoid.fbx` and baked to project-owned Generic transform clips. Actor planar root translation/yaw are separate curves; the body clip excludes the actor GameObject transform. Vendor FBXs/importers and canonical `lara.fbx` remain read-only.

Retarget bake setting: Unity Humanoid Foot IK **enabled** to match the accepted audit appearance; the resulting joint transforms are baked. Production playback is Generic and runs no runtime Foot IK.

Body ownership: facial transforms below `head`, plus the `upperFaceRig`/`lowerJaw` facial subtrees, are excluded from sampling and emitted curves. Head, neck, body and finger animation remain included. No blendshape curves are emitted. Every bake replaces all configured clips in place and clears their previous curves, including facial curves from older bakes.

Default production playback speed: **0.67×** for both body and authored trajectory. Full Start/Stop distance threshold: **3.93 m**.

| Motion | Source | Duration (s) | Root X (m) | Root Z (m) | Yaw (°) | Path (m) | Entry support | Exit support |
|---|---|---:|---:|---:|---:|---:|---|---|
| Walk01_Start_A | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Walk01_Start.FBX` | 2.333 | -0.017 | 2.019 | 5.7 | 2.025 | Both | Neither |
| Walk01_Start_B_Mirrored | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Walk01_Start.FBX` | 2.333 | 0.017 | 2.019 | -5.7 | 2.025 | Both | Neither |
| Walk01_Loop | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Walk01.FBX` | 1.333 | 0.000 | 1.453 | 0.0 | 1.455 | Neither | Neither |
| Walk01_Stop_A | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Walk01_Stop.FBX` | 3.000 | -0.186 | 2.023 | -5.7 | 2.042 | Neither | Neither |
| Walk01_Stop_B_Mirrored | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Walk01_Stop.FBX` | 3.000 | 0.186 | 2.023 | 5.7 | 2.042 | Neither | Neither |
| TurnLeft90 | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_TurnLeft_90.FBX` | 1.800 | -0.008 | -0.005 | -90.0 | 0.141 | Neither | Both |
| TurnRight90 | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_TurnRight_90.FBX` | 1.800 | 0.005 | -0.006 | 90.0 | 0.215 | Left | Both |
| TurnLeft180 | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_TurnLeft_180.FBX` | 2.667 | -0.001 | -0.011 | -180.0 | 0.485 | Neither | Both |
| TurnRight180 | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_TurnRight_180.FBX` | 2.667 | -0.001 | -0.011 | 180.0 | 0.427 | Left | Both |

## Automatically measured phase matches

Start A → Loop: **0.000**; mirrored Start B → Loop: **0.500**.
Stop A entry phase: **0.000**; mirrored Stop B entry phase: **0.500**.

In-motion reversals use a phase-matched authored Stop, then an in-place 180° turn clip, then a new Start. The 180° clips are not phase-baked into the walking cycle.
Phase matching minimizes local transform position/rotation error and root linear/yaw velocity difference at each Start/Loop and Loop/Stop seam. Support-foot labels are height/relative-velocity heuristics. Inspect every seam visually before treating these estimates as accepted production timing.
