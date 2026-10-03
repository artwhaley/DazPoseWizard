# Magic art pass

The five Cast presets run for **3.0 seconds**. Their burst particles live at least as long as the event. Individual particles build up at slightly different times, with a sustained middle and a fade over the last second. Aura uses continuous emission and individual particle fades. The revised direction is photographic light, fine airborne particles, and turbulent vapor, using the user's Harry Potter film reference.

## Apply the pass

Outside Play Mode, run **Tools > DAZ Pose > Magic > Generate Starter Magic Assets** once. This command refreshes all five graph layouts and their Style presets, including already-generated assets. It keeps the same VFX asset paths and GUIDs, so the catalog and preset references continue to point at them. No asset deletion is needed.

The command regenerates the project-owned sprites in `Assets/DazPose/Effects/Magic/Shared/` and reapplies the starter Style values in `Assets/DazPose/Generated/Magic/Styles/`. Running it again replaces manual edits to those starter graph layouts, sprites, and Style values. Inspector adjustments can be made after generation for visual tuning. The graphs embed the HLSL source in their Custom HLSL blocks: editing the shared `.hlsl` alone does not refresh already-generated graphs, so this regeneration step is necessary.

## Art direction

| Family | Cast | Aura |
| --- | --- | --- |
| Emberfire | Small rolling fragments of flame, ivory/gold hot points, buoyant orange embers and restrained charcoal smoke. | Ongoing uneven convection and embers, with fragmented flame and light smoke. |
| Rift Bloom | Dusty pink/violet vapor gathers inward and releases unevenly, carrying small bright particles. | Turbulent pockets of colored vapor and independently drifting points. |
| Arc Cyan | Blue-white charged dust, very short spark shapes and local irregular flicker within thin cyan vapor. | Faster, restless particles with independent flicker and tiny local excursions. |
| Verdant Pulse | Pale jade light and fine motes carried upward through irregular, opening currents. | Gentle ascending luminous dust and thin green vapor. |
| Violet Serenity | Suspended smoky lavender vapor with slow, uneven drift and restrained ivory highlights. | Quiet mist with sparse soft points and individually phased breathing. |

Connected ribbons, long bolts, floor rings, rising halos and star-shaped glints have been removed from the generated layouts. Compact wisps use warped fractal noise with eroded edges rather than a repeated clean curl. Hot points are small and relatively neutral; softer colored fringes carry the family palette. Flame fragments remain compact, and motion follows independent smooth paths through a turbulent field rather than connected target-height curves.

One GPU simulation feeds four output layers: motes, small glows, luminous vapor/flame, and translucent smoke. Cohorts select their own output locally; hiding a cohort from one output does not kill its simulation. The capacity remains 2048 particles per instance; casts use 512–768 particles depending on family. The shader entry points are self-contained because this VFX Graph version embeds only the selected Custom HLSL function. Integer seeds are reduced before conversion to floats to preserve per-particle variation.

`TargetBase`, derived from the actual renderer bounds, sets the lower bound of rising particles. All layers continue to follow the live target in world space. Vapor uses textured quads with depth fading over 3.5 cm; smoke fades over 6 cm, reducing hard intersections with the body and floor. These are layered particle sprites, not a volumetric fluid simulation.

## Manual visual review

1. Generate the assets, enter Play Mode in the existing performance scene, and use the Magic panel. Cast each style once on Lara, allowing it to complete before choosing the next. Each should remain readable across its three-second event and fade without a final pop.
2. Check the family-specific motion in the table from the current player camera. Pink should gather and release; cyan should feel restless and locally charged; green should rise gently; purple should be visibly calmer. Look for fine luminous particles and broken vapor, with no long lines or graphic rings. Lara's face and body should remain readable.
3. Run each Aura for at least ten seconds. Check the ongoing movement, gradual arrival and departure of individual particles, and the absence of a synchronized reset of the entire effect. Clear Aura and inspect the fade.
4. Walk and turn Lara with an Aura active. Ground layers should follow her feet and rising layers should keep world-up motion. Repeat one Cast on the external target.
5. Leave one Aura active and overlap two different Casts. Their instances should coexist and clean themselves up without removing the Aura.

No Unity launch, shader compilation, or Play Mode run was performed for this pass; the rendered result still needs the user's visual review.

## References

MPC's [Harry Potter and the Philosopher's Stone breakdown](https://www.mpcvfx.com/en/filmography/harry-potter-and-the-philosophers-stone/) describes combining particle animation, light effects and texture for the film's spells. Fernando Cuevas's [Magic Wand FX study](https://www.sidefx.com/gallery/harry-potter-magic-wand-fx/) layers particle simulations for a film-inspired result. Those are visual references; this realtime implementation uses the project's existing VFX Graph pipeline and a much smaller particle budget. The choice of fine motes, turbulent vapor and restrained palettes is our interpretation of the user's requested direction.
