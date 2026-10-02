# KAWAII seating bake for Generic Lara

Source clips were sampled at 60 fps through the validated Human Avatar on `laraHumanoid.fbx`, then baked to project-owned Generic transform clips. The actor-root trajectory and actual Humanoid Hips/pelvis offset are stored separately. The baker does not modify canonical `lara.fbx`, either model importer, or vendor FBXs.

The Cross Legs body clips share a skeleton-root offset of **(0.000, 0.346, 0.000) m** so the first Cross Legs frame keeps the pelvis at the `Sit_Start` seat contact (measured residual: **0.000 m**). This rebases the child skeleton root in the Generic body clips; the performer GameObject root remains locked throughout Cross Legs Start, Loop, and End. Their measured actor-root trajectories remain diagnostic data and are not applied at runtime.

Basic seated state: **hold the final `KA_Sit_Start` frame** so downstream pose, breathing, gaze, expressions, and blink remain active. `KA_Idle10_Sit_Loop` is baked only as an audition candidate; this source-only pass cannot claim visual seam acceptance.

Body ownership: facial transforms below `head`, plus the `upperFaceRig`/`lowerJaw` facial subtrees, are excluded from sampling, seam scoring and emitted curves. Head, neck, body and finger animation remain included. No blendshape curves are emitted. Every bake replaces all configured clips in place and clears their previous curves, including facial curves from older bakes.

| Motion | Source | Duration (s) | Root X (m) | Root Y (m) | Root Z (m) | Root yaw (°) | Final pelvis offset (m) |
|---|---|---:|---:|---:|---:|---:|---:|
| Sit_Start | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Sit_Start.FBX` | 3.000 | 0.000 | -0.346 | -0.289 | 0.0 | (0.000, 0.939, -0.097) |
| Sit_End | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Sit_End.FBX` | 3.333 | 0.000 | 0.346 | 0.289 | 0.0 | (0.000, 0.702, 0.016) |
| Sit_CrossLegs_Start | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Sit_CrossLegs_Start.FBX` | 2.000 | -0.023 | 0.027 | 0.026 | -16.9 | (-0.009, 0.919, -0.125) |
| Sit_CrossLegs_Loop | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Sit_CrossLegs_Loop.FBX` | 13.000 | 0.000 | 0.000 | 0.000 | 0.0 | (-0.009, 0.946, -0.125) |
| Sit_CrossLegs_End | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Sit_CrossLegs_End.FBX` | 3.000 | 0.014 | -0.027 | -0.031 | 16.9 | (0.000, 0.967, -0.097) |
| Idle10_Sit_Loop_Candidate | `Assets/KAWAII_ANIMATIOMS_100/Assets/Animations/@KA_Idle10_Sit_Loop.FBX` | 5.000 | 0.000 | 0.000 | 0.000 | 0.0 | (0.001, 0.317, -0.114) |

## Automatically measured transition seams

`Sit_Start` end → Basic hold: exact same sampled frame (zero pose discontinuity by construction).

`CrossLegs_Start` end → rotated `CrossLegs_Loop` entry: source loop phase **0.001**, pose score **0.006**. The generated loop clip is rotated so this best-matching frame becomes time zero.

`CrossLegs_Loop` → `CrossLegs_End`: numerically best-matching generated loop phase **0.999**; pose score **0.000**.

Runtime P0.B1 no longer waits for this seam. It freezes the current loop pose and blends to frozen `CrossLegs_End` frame 0 over `CrossLegsExitBlendSeconds` gameplay seconds (default **0.5 s**, independent of PlaybackSpeed). Only after preparation finishes does the End clip advance from time 0. The measured seam metadata remains diagnostic information.

Basic hold → `Sit_End`: start source phase **0.000** to best match the held Basic pose; pose score **0.959**.

Audition-only `KA_Idle10_Sit_Loop` best entry phase: **0.558**. It is not selected automatically.

These are numerical transform matches, not a visual quality verdict. In Tools > DAZ Pose > Seating > Edit Mode Preview, use Play Sit Down from Approach and Show Seated End Pose to tune the anchors. Sit Start and Sit End use the same actor root calculation as playback, including final anchor correction. Disable Apply playback final correction to inspect the natural landing; Fit Approach to Sit Start moves the actual ApproachAnchor so the natural endpoint meets SeatAnchor. Seated body clips hold the actor root fixed. Then in Play Mode inspect: standing → Sit_Start → Basic hold; Basic → CrossLegs_Start → loop; loop → CrossLegs_End → Basic; and Basic → Sit_End → standing. Keep the loop running for several cycles and check whether Lara drifts relative to the seat.

