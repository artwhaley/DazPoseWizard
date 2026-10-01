# Animation Audit Inventory

Generated from the installed project assets by `Tools > DAZ Pose > Animation Audit > Build Catalog and Inventory`. The package folders are scanned read-only. Import flags and curve names are inventory hints; they do not prove visual suitability or retarget quality.

Canonical Lara model: `Assets/TestCharacter/lara.fbx`. Its source FBX importer was not changed by the audit tool.

## KAWAII

Source folder: `Assets/KAWAII_ANIMATIOMS_100` (present)

Discovered AnimationClip assets/sub-assets: **416**

| Category | Count |
|---|---:|
| Combat | 30 |
| Directional Walk | 39 |
| Forward Walk | 27 |
| Idle | 84 |
| Jump | 7 |
| Other | 44 |
| Run | 24 |
| Sit / Seated | 58 |
| Sleep / Lie | 22 |
| Speak / Body Gesture | 33 |
| Turn / Pivot | 48 |

## Female Movement Animset Pro / Kubold

Source folder: `Assets/FemaleMovementAnimsetPro` (present)

Discovered AnimationClip assets/sub-assets: **171**

| Category | Count |
|---|---:|
| Directional Walk | 24 |
| Forward Walk | 43 |
| Idle | 9 |
| Jump | 13 |
| Other | 30 |
| Run | 39 |
| Sit / Seated | 3 |
| Turn / Pivot | 10 |

## Priority clips not found

**KAWAII:** KA_Sit_Loop, Idle01_breathing, Idle09_Waiting, Idle11_LookingBack, Idle12_LeaningForward, Idle18_Shy, Idle37_Tsundere, Idle39_CuteArmUp, Idle40_CrossLegs, Idle41_CuteShyPose, Idle43_HandOnHip, Idle45_WaveHandSlightly, Idle50_StandingTalk1_1, Idle51_StandingTalk1_2, Idle52_Curtsy, Idle65_ThumbsUp, Idle72_LeanForward, Idle73_IdolPose, Idle75_Pointing

**Kubold:** all expected priority stems found

## Duplicate clip display names

- FemaleMovementAnimsetPro / `bind`: `Assets/FemaleMovementAnimsetPro/Animations/FemaleMovementAnimsetPro_1.fbx`, `Assets/FemaleMovementAnimsetPro/Animations/FemaleMovementAnimsetPro_2.fbx`

## Import errors and warnings

No importer error or warning strings were exposed by the scanned ModelImporters.

## Retarget and runtime status

Catalog entries retain actual Unity `AnimationClip` objects, including multi-clip Kubold FBX sub-assets. Runtime retarget/playback is deliberately recorded as unverified until the audit scene evaluates each clip on Lara. Root motion and mirror measurements must be taken in Play Mode; this report does not infer them from filenames or import flags.

