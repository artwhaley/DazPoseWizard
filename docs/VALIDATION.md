# Blender visual evaluation

Automated tests check the DSON reader, fixture facts, target resolution, rotation math, validation rules, and output structure. They do not prove geometric accuracy. Visual validation remains pending until the generated pose is compared with the DAZ-exported posed reference FBX.

1. Publish the app and convert the Cherish preset to `.dazpose.json`, `.bvh`, and `.report.txt`.
2. Open a clean Blender scene.
3. Import the DAZ-exported Cherish reference FBX.
4. Import the generated BVH as an armature. Use the native BVH rotation mode; if needed, scale the centimeter file by `0.01` on import and select `-Z Forward`, `Y Up`.
5. Compare hip, pelvis, thighs, shins, feet, abdomen/spine, shoulders, forearms, and head first; inspect fingers and hands afterward.
6. Record consistent differences without editing the DAZ data to force a match.

Interpretation guide:

- A consistent whole-body mirror or rotation suggests an exporter coordinate convention issue.
- A correct root with diverging limbs suggests hierarchy, orientation, or Euler-order behavior.
- Correct large joints with finger/twist differences may reflect BVH's loss of Genesis joint-orientation or bone-roll information.
- If the canonical pose looks coherent but BVH differs, investigate the BVH exporter separately.

Do not describe geometric fidelity as verified until this comparison is complete.
