# Unity visual evaluation

Automated tests check the DSON reader, fixture facts, target resolution, rotation math, validation rules, and output structure. They do not prove geometric accuracy. Visual validation remains pending until the generated pose is compared with the DAZ-exported posed reference FBX.

1. Convert the Cherish preset to `.dazpose.json`; this is the canonical fidelity representation.
2. Open `validation/DazPoseUnityValidation` in Unity 6000.5.9f1.
3. Open `Assets/Scenes/PoseValidation.unity`, select the Lara root, and run `Tools > DAZ Pose > Inspect Selected Character`.
4. Run `Tools > DAZ Pose > Apply Pose to Selected Character` and select the generated Cherish `.dazpose.json`.
5. Compare hip, pelvis, thighs, shins, feet, abdomen/spine, shoulders, forearms, and head first; inspect fingers and hands afterward.
6. Record consistent differences without editing the DAZ data to force a match. BVH is not used in this validation path.

Interpretation guide:

- A high rest-landmark fit residual suggests an FBX hierarchy, skeleton asset, root scale, or coordinate boundary mismatch. Do not apply pose data until that report is understood.
- If the root aligns but limbs diverge, investigate DSON evaluation, coordinate conversion, and FBX rest-basis mapping separately.
- Correct large joints with finger/twist differences may reflect missing secondary pose data or a remaining rest-basis mismatch.

Do not describe geometric fidelity as verified until the user has visually compared the applied pose with the independent DAZ reference.
