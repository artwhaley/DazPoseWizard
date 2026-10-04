# Magic art pass — DissolveTo fireflies

Emberfire, Rift Bloom, Arc Cyan and Verdant Pulse now use the accepted **DissolveTo particle appearance**. Violet Serenity is preserved.

## Apply

Outside Play Mode, run **Tools > DAZ Pose > Magic > Generate Starter Magic Assets** once.

The command refreshes the four revised graphs and Style presets in place, retaining their paths and GUIDs. It keeps the existing Violet Serenity graph, embedded HLSL, Style, Spell and Aura. Existing shared textures are retained as well, so regeneration does not alter the purple effect's masks. No asset deletion is needed.

The four graphs embed their HLSL; editing the shared source alone does not refresh their existing embedded code. The menu command performs that refresh. It replaces manual graph/preset edits for those four starter families.

## Exact appearance reference

The generator reads the current appearance values from:

`Assets/DazPose/Effects/Dissolve/FirstContactDissolveProfile.asset`

Its current values are:

| Property | DissolveTo and the four revised families |
| --- | --- |
| Sprite, for both outputs | `Assets/DazPose/Effects/ParticleBody/FireflyGlow.png` |
| Blend | Alpha, matching `PerformerParticleBody.vfx` |
| Core size | 0.003 m, with the same seeded 0.65–1.0 size variation |
| Glow size | 0.009 m, with the same seeded size variation |
| Core RGB / opacity | White HDR 3.2 / 0.85 |
| Glow peak RGB / opacity | HDR 2.8 / 0.5; hue varies by family |
| Intensity multiplier | 1.0 |

Every particle renders both its white core and its colored glow. The core/glow sizing and opacity math follows `PerformerParticleBodyCoreOutput` and `PerformerParticleBodyGlowOutput`. The revised four have two output layers, with no flame, smoke, vapor, streak or ring sprites.

Values are copied when the menu runs; later edits to the Dissolve Profile are incorporated on the next regeneration. The existing DissolveTo assets and implementation are unchanged.

## Colors and behavior

| Family | Color | Movement |
| --- | --- | --- |
| Emberfire | Orange/gold around a white core | Rising embers follow individual bowed convection paths and small curling gusts. |
| Rift Bloom | Magenta/pink around a white core | Particles gather toward the target, then peel upward and outward along curved paths. Cast tightens and releases the pattern. |
| Arc Cyan | Cyan/blue around a white core | Quick short flights within slowly drifting charge pockets; independent clocks, with no connected bolt geometry. |
| Verdant Pulse | Green around a white core | Slower, opening upward currents of discrete fireflies. |
| Violet Serenity | Existing accepted purple | Existing accepted behavior and rendering. |

The four revised families use the same cubic arc and individual flutter construction as DissolveTo. Particle paths are phased independently and fade before returning to their starting point. Cast lasts **3.0 seconds**, with a buildup and final fade; Aura emits continuously until cleared.

The four graphs have capacity for 8192 particles per instance. Cast emits 4096 cyan particles or 6144 particles for the other three. Aura emits 1500–1700 particles per second. The greater count retains visible density at the much smaller accepted firefly size. Violet retains its existing count and capacity.

All instances follow the live target and use its renderer bounds, including the unpadded `TargetBase` for the lower boundary.

## Inspector controls

In `Assets/DazPose/Generated/Magic/Styles/`, the four revised Style assets expose:

- **Primary Color:** white core color and opacity.
- **Secondary Color:** colored glow and opacity.
- **Particle Size:** core size.
- **Particle Glow Size:** glow size.
- **Spell Burst Count / Aura Spawn Rate:** density.
- **Rise Speed:** travel rate; cyan uses short local flight cycles.
- **Swirl Strength / Turbulence:** curl and small individual path deviations.
- **Pulse Frequency:** individual shimmer rate.
- **Spell Duration / Aura fades:** lifetime and transitions.

Violet's existing graph uses its original controls; the added Particle Glow Size input applies to the four revised firefly graphs.

## Manual visual review

1. Regenerate and cast the revised four on Lara from the current player camera. Compare their individual dots directly with DissolveTo: the same size, bright white cores and soft colored edges should be apparent.
2. Compare their movement with the table. Orange rises, pink gathers/releases, cyan moves in fast local pockets, and green rises more slowly. Check their full three-second event and ending fade.
3. Confirm Violet Serenity looks exactly as it did before regeneration.
4. Run each Aura for at least ten seconds, then clear it. Watch for ongoing motion, gradual individual fades, and no visible phase-reset jumps.
5. Walk and turn with an Aura active. Repeat one Cast on the external target.
6. Overlap two different Casts with an Aura. Their instances should coexist and clean themselves up independently.

No Unity launch, shader compilation or Play Mode run was performed. The rendered result needs the user's visual review.
