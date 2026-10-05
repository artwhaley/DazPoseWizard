# Lara asset storage

Checked 2026-10-04. `Assets/TestCharacter` contains 1.874 GiB. Its FBXs are
ignored, and no FBX in this folder is tracked. Git LFS 3.7.1 is installed with
system filters, but there are no repository LFS attributes or tracked LFS files.
The current policy is a local licensed source library, not an LFS library.

The ignore rule now covers **all** `TestCharacter/*.images/` export directories,
including clean-reference and future outfit textures. Importer `.fbx.meta`
files can still be versioned to retain model GUIDs and rig configuration.
Generated candidate assets, evidence, isolated batch projects and Unity caches
are also ignored. An ignored asset is not recoverable from a Git clone: keep
independent source backups. Do not force-add these sources casually.

## Retention decisions

| Export | Keep in active project? | Reason |
| --- | --- | --- |
| `lara.fbx` | Yes | Original scenes, particle bindings, canonical morph order and candidate rebuild. |
| `laraHumanoid.fbx` | Yes | Humanoid test scenes and retarget/baking tools use its configured Avatar. The bytes duplicate `lara.fbx`, but its importer is different. |
| `l.aranudeclosed.fbx` | Yes | Candidate preparation and installation use the closed reference. |
| `larafirstoutfit.fbx` | Yes | Candidate rebuild obtains facial/anatomy channels here; first outfit rebuild also uses it. |
| `larasecondoutfit.fbx` | Yes | Active layered outfit and heel import. |
| `laracleanreference.fbx` | Yes | Active clean-body comparison. Raw inspection confirms 16,556 points / 16,368 polygons; only one exported facial channel. Transfer/validation of other channels is still needed before treating it as a functioning body replacement. |
| `laranude.fbx` | Archive candidate | Earlier exploratory export, superseded by the approved closed/first-outfit reconstruction. No serialized model GUID reference outside its own importer, and no current runtime/builder source-path use found. Model plus export textures: about 238.5 MiB. |
| `l.aranudeopen.fbx` | Archive candidate | Captured opening is retained in the generated endpoint manifest and approved body. No serialized model GUID reference outside its own importer; retain source for rerunning endpoint exploration. Model plus export textures: about 219.9 MiB. |

The last two bundles could remove about **458 MiB** from the active folder.
This audit did not move or delete them. Keep their FBX, `.fbx.meta`, `.images`
directory and `.images.meta` together when archiving. Copy to a dedicated folder
such as `F:/DazPoseWizardAssetArchive/Lara/2026-10-04`, verify relative paths,
lengths and SHA-256 hashes, then remove originals only after verifying asset
references again. Record original project paths in the archive manifest. Restore
the same paths and metadata before rerunning an old source-based investigation.
Keep source DUFs and content-library requirements documented too.

Do not deduplicate `lara.fbx` and `laraHumanoid.fbx` by deleting either: their
different GUIDs and importer configurations have distinct purposes.

## Growth beyond this folder

Across the export-image folders, 123 texture files have only 36 unique hashes:
about **568 MiB** are repeated bytes. The converter resolves source textures from
the DUF/content library; future exports can avoid redundant texture copies once
that export setting is proved with the user. Existing importer/material texture
GUID dependencies must be checked before removing copies.

Git itself has about 694 MiB of loose objects. The largest currently tracked
files include vendored Unity AI Assistant executables (approximately 451 MiB
combined), a 96 MB generated action animation, and existing vendor animation
FBXs. Adding an ignore rule does not remove already tracked files or shrink
history. Any package relocation, LFS migration or history cleanup is a separate
repository maintenance task; do not rewrite history as part of a wardrobe import.

Unity's main `Library` cache is about 5.94 GiB and is ignored. It affects disk
usage but not Git history. The isolated batch project also has its own copies;
do not remove active diagnostic inputs/cache while Unity is using that project.
