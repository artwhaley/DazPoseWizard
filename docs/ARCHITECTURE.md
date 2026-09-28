# Architecture

`DazPose.Core` owns DSON loading, Genesis node parsing, pose URL parsing, static-pose validation, transform evaluation, and export. `DazPose.App` is a small Avalonia code-behind shell that calls the core on a worker task. The converter does not depend on DAZ Studio, Unity, Blender, or a database.

## Parsing and addressing

`DsonFileReader` detects gzip from the file signature and then parses UTF-8 JSON. The figure parser keeps both node ID and node name indexes because hierarchy links use IDs while the Cherish preset addresses bones by name. Parent links are validated and the hierarchy is checked for cycles.

Pose URLs are parsed centrally. Skeletal rotation and translation channels resolve by `name://` node name first; an ID fallback is explicit in diagnostics. Unresolved skeletal targets, multi-key skeletal channels, and non-neutral unsupported properties stop conversion. Neutral unsupported channels are retained in diagnostics and listed in the report.

## Transform evaluation

Source values stay in centimeters and degrees in right-handed, Y-up DAZ space. Animated rotations use each node's declared fixed-axis Euler order (the listed axes are applied in sequence); node orientation is converted as XYZ and conjugates the animated rotation. World rotation, scale inheritance, and hierarchical center offsets are evaluated in `DazTransformEvaluator`, so exporters do not reinterpret DAZ sliders. The implementation follows the relationships in the [official DSON node transform specification](https://docs.daz3d.com/public/dson_spec/object_definitions/node/start).

Node scale values and `inherits_scale` are retained and included in the evaluated transforms. The supplied G8F definition uses unit scale for this fixture; scale-heavy content is not covered by V0 fixture validation.

## Export

The deterministic `.dazpose.json` is canonical and contains source asset IDs, source bone fields, raw pose channels, ignored neutral channels, and evaluated local/world rotations and positions. The `.bvh` is a one-frame preview rooted at `hip`, uses centimeter offsets, and declares X/Y/Z rotation channels after XYZ quaternion decomposition. It may lose joint orientation and bone-roll detail, so it must not be used to revise the canonical pose values.

Every successful conversion also creates a text report with source/output paths, asset IDs, node/target counts, ignored channels, timestamp, and warnings.
