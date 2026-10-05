# Manual grip verification

Use the existing isolated project `.dazposewizard/handgrip-validation/project`.
The runtime implementation is also in the main project; this copy keeps manual
testing separate from the editor used for other work.

Close the isolated editor before updating its copy. From the repository root,
run `./scripts/open-handgrip-manual.ps1` in your normal Windows PowerShell. It
copies only grip sources, their integration files, profile, scene and package
definitions, then opens Unity 6000.5.9f1. It does not regenerate Lara or the scene.

1. Wait for compilation. If needed use Assets > Refresh (Ctrl+R). Check Console
   for compiler errors, then open `Assets/Scenes/HandGripAcceptance.unity` if it
   is not already open. Do not run the scene creation or calibration commands.
2. Press Play. In Game view scroll the existing runtime panel to HAND GRIP
   ACCEPTANCE. Enable Gizmos in Scene view to see the analytic rod and probes.
   The rod is a diagnostic wire fixture; its contact surface is analytic.
3. Click Move Near, then GRIP ON. Observe acquisition and Active without walking.
   Start Sine should move the palm along the rod. Stop / Hold should freeze the
   source position; Restart restarts the selected source. The MOTION DRIVER
   section has the existing Funscript selection and playback controls.
4. Use Position 0 / .25 / .5 / .75 / 1 while stopped. Try radius, tilt and wrist
   twist. Watch palm error, reach, each digit's status and finger/skin contact.
5. While gripping, click Move Far. Expect release, walking and reacquisition.
   Impossible Height should give an explicit failure. Move Near and GRIP ON
   retries; RELEASE GRIP should blend out and leave Lara where she stands.
6. Release first, enable RAW DIGIT CALIBRATION, and move one anatomical digit
   slider at a time. Check inward curl, flipping and thumb spread/opposition.
   Contact is disabled in this mode. Disable it before normal grip testing.
7. For arbitrary target edits, select HandGripFixture or its Start/End children
   in Scene view during Play. The GripContactRod inspector exposes its normal
   reference. These Play edits are temporary.

Report the button sequence, state/failure, source and position, radius/tilt,
anatomical digit, and a screenshot or short description of the visible problem.
Thumb calibration uses an opposed open pose; visual approval of that
choice and full skinned contact remains pending. Automated probe contact checks
do not establish skin clearance or artistic quality.

After the first manual review, wrist twist was expanded to -90° through +90°
with a reset button. Twist now orbits the palm contact anchor around the cylinder
along with its orientation. The thumb endpoint calibration now starts opposite
the four fingers and wraps from the other side, rather than aiming toward the
palm center with the old spread pose. It retains its own four contact probes and
independent stop curl. `python scripts/check-thumb-candidate.py` checks the saved
thumb endpoints and nonpenetrating analytic contact at 15/25/35/55 mm radii.
This is a candidate pending the next skin/visual review. Recompile, leave and
reenter Play to reload the profile into the grip job, then try nominal radius
first and sweep wrist twist. The existing reach planner may reposition the actor
if the changed wrist pose cannot be reached from the current stance.

Previous executor evidence: 52 geometry/spatial assertions and 16 live gates
passed in the isolated project. These are historical evidence, not manual
acceptance. This preparation adds playback shortcuts and clearer diagnostics;
it does not claim the handoff's remaining robustness/diagnostic backlog is done.
