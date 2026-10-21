# Context and Product Intent

The first major DazPoseWizard sprint built the performance vocabulary: Lara can pose, gesture, speak, look, move, sit, dissolve, grip/follow motion, control toys, change wardrobe, and the player view can move/look/track. The accepted authoring seam is a saved `PerformanceHost` executing a `PerformanceSequence` against a scoped `PerformanceRun`.

The next layer is a spiritual successor to Virtual Succubus.

The eventual user experience is:

- choose one of a small number of scene types;
- each scene has a recognizable authored dramatic shape;
- selected portions of the scene are filled by reusable cards;
- a Director later chooses cards based on mood, progress, preferences, equipment, physical state, memory, and current intent;
- repeated sessions vary without abandoning authored pacing;
- assignments and important facts can survive between sessions.

This packet builds only the **foundation needed before cards and the Director**.

## Key architecture

The long-term execution chain is:

```text
Persistent Player Profile
        ↓
Session Contract + Session/World State
        ↓
Serialized Scene Score
        ↓
Story Runner
        ↓
(later) Director Window → Card Selector → Card Program
        ↓
existing PerformanceRun/scoped vocabulary
```

The scene score owns the authored dramatic spine. The Director, when added later, only owns bounded "director windows."

## First Contact D1–D5 boundary

The First Contact scene ultimately includes:

- setup/intro;
- kink negotiation;
- equipment negotiation and selection;
- clothing negotiation;
- warmup cards;
- main cards;
- equipment-specific card;
- climax;
- resolution/assignment.

D1–D5 do **not** implement cards or the Director.

Therefore the D1–D5 First Contact serialized program shall execute the intro/negotiation/state work and end at an explicit `story.checkpoint` immediately before the first Warmup Director fill.

That checkpoint is not a fake Director. It is an honest sprint boundary.
