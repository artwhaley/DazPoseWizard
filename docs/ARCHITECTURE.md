# Architecture

`DazPose.Core` owns DSON loading, Genesis node parsing, pose URL parsing, static-pose validation, transform evaluation, and export. `DazPose.App` is a small Avalonia code-behind shell that calls the core on a worker task. The converter does not depend on DAZ Studio, Unity, Blender, or a database.

## Parsing and addressing

`DsonFileReader` detects gzip from the file signature and then parses UTF-8 JSON. The figure parser keeps both node ID and node name indexes because hierarchy links use IDs while the Cherish preset addresses bones by name. Parent links are validated and the hierarchy is checked for cycles.

Pose URLs are parsed centrally. Skeletal rotation and translation channels resolve by `name://` node name first; an ID fallback is explicit in diagnostics. Stage 5 also preserves static `@selection#...:?value/value` figure controls with both raw URL identity and a percent-decoded display name. Unresolved skeletal targets, multi-key skeletal channels, and non-neutral unsupported properties stop conversion. Neutral unsupported channels are retained in diagnostics and listed in the report.

## Transform evaluation

Source values stay in centimeters and degrees in right-handed, Y-up DAZ space. Animated rotations use each node's declared fixed-axis Euler order (the listed axes are applied in sequence); node orientation is converted as XYZ and conjugates the animated rotation. World rotation, scale inheritance, and hierarchical center offsets are evaluated in `DazTransformEvaluator`, so exporters do not reinterpret DAZ sliders. The implementation follows the relationships in the [official DSON node transform specification](https://docs.daz3d.com/public/dson_spec/object_definitions/node/start).

Node scale values and `inherits_scale` are retained and included in the evaluated transforms. The supplied G8F definition uses unit scale for this fixture; scale-heavy content is not covered by V0 fixture validation.

## Export

The deterministic `.dazpose.json` is canonical and contains source asset IDs, source bone fields, raw pose channels, ignored neutral channels, evaluated rest and pose local/world rotations and positions, and (format v2) renderer-agnostic figure controls. It never stores Unity renderer paths. Existing format v1 skeletal files remain supported. The rest snapshot is the minimum information needed to compare DAZ coordinates with the actual Unity-imported FBX skeleton. The `.bvh` is a one-frame, approximate interoperability preview rooted at `hip`; linear offsets and translations are converted from DAZ centimeters to meters. It may lose joint orientation and bone-roll detail, so it must not be used to revise canonical pose values.

Every successful conversion also creates a text report with source/output paths, asset IDs, node/target counts, ignored channels, timestamp, and warnings.

## Unity validation boundary

`validation/DazPoseUnityValidation` consumes only `.dazpose.json`; it does not parse DSON. It resolves the Genesis 8 Female skeleton beneath `SkeletonRoot`, while emitted clips bind relative to the common character `BindingRoot`, which can address both the skeleton and sibling renderers/accessories. Skeletal resolution uses exact DAZ names/IDs and parent chains to disambiguate duplicate FBX transforms, then fits one 3×3 DAZ-to-Unity basis from major rest-bone positions. The fit is allowed to have determinant -1 when the paired imported positions establish a reflected basis. Static figure controls prefer an exact imported blendshape name, then resolve the exact `<figure-root-name>__<control-name>` name Unity creates during DAZ FBX import across every `SkinnedMeshRenderer` under `BindingRoot`; one control can produce multiple renderer bindings. Direct Apply, clip generation, parity, preview restoration, and runtime checks share these resolved values. Zero-match controls stop with a reference-refresh diagnostic; ERC/formula interpretation remains unsupported.

The Unity project-level `.dazposewizard/required-morphs.json` stores morphs required by converted content and categorized always-export pins. The deterministic CSV emits enabled pins first in category order, then content-only names, once per exact name, followed by one `Anything,Bake` fallback. Import it manually into DAZ Studio. Always-export pins only control FBX morph export; they do not create `.anim` clips. After a reference FBX import, the browser import processor retries pending/failed canonical jobs; **Tools > DAZ Pose > Process Pending Browser Imports** also forces a retry.
