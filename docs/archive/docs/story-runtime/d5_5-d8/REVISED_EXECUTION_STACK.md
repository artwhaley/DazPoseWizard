# D5.5–D8 REVISED EXECUTION STACK
## First Contact — full authored session

---

# 0. AUTHORITY

This packet supersedes the earlier D5.5 corrective packet wherever they conflict.

It also supersedes any D1–D5 instruction that allows the First Contact scene to use:

- representative dialogue;
- representative movement;
- approximate choreography;
- substitute poses;
- substitute scene transitions;
- omitted lines because audio is unavailable;
- verbose JSON as the normal human authoring surface.

The source of truth for D8 is the user's complete First Contact script, beginning:

> Scene Setup: Lara starts hidden...

and ending:

> "...behave. Or don't. I get off either way. Bye!"

The target is **the complete playable session**, not merely the Intro Card.

---

# 1. PRODUCT GOAL

At the end of D8, the saved production scene must run this complete shape:

```text
Scene Setup
    ↓
Intro / negotiation
    ↓
Warmup phase
    ↓
Main phase
    ↓
Climax phase
    ↓
Resolution phase
    ↓
session completes
```

The author-facing source should remain recognizably close to the user's original pen-and-paper notation.

An author should see things like:

```text
await Lara.DissolveIn(lara_start)
Camera.Track(Lara)

await Lara.Say("Hi there...")

await Lara.PersistentPose(SexyVamp)

await Popup.Show(Kinks)

phase Warmup
{
    while Completion < 40%
        await Director.DrawAndExecute(tags: [Warmup])
}
```

They should **not** normally see:

```json
{
    "id": "...",
    "op": "...",
    "target": "...",
    "asset": "...",
    "number": 0,
    "flag": false
}
```

---

# 2. AUTHORING ARCHITECTURE — LOCKED

The architecture from this point forward is:

```text
Human-authored .dazstory
            │
            ▼
      parser/compiler
            │
            ▼
flat typed executable program
            │
            ▼
        StoryRunner
            │
            ▼
      PerformanceRun
            │
     ┌──────┼────────┐
     ▼      ▼        ▼
   Lara   Player   services/state
```

Cards add:

```text
Card metadata
    +
compiled card body
        │
        ▼
    CardCatalog
        │
        ▼
     Director
        │
        ▼
    StoryRunner
```

The domain objects are not allowed to depend upon the `.dazstory` storage format.

`.dazstory` is one authoring frontend.

A future SQLite editor, Blocks Engine editor, WPF editor, or other tool must be able to create the same domain objects without rewriting the runtime.

---

# 3. IMPORTANT DOMAIN DECISIONS THAT REMAIN LOCKED

## Submission score

Do not revisit it.

Do not rename it.

Do not change its calculation.

Do not split it into control/favor/trust in this sprint.

Use the current concept exactly as the user requested.

---

## Equipment

Keep the hybrid model:

### Universal state

Small and stable:

```text
Posture
Hands
Ankles
Oral
Anal
Genital
clothing state
```

### Open-ended equipment/tags

Examples:

```text
equipment:rope
equipment:butt-plug
equipment:dildo-anal
equipment:ice
```

Adding a new equipment type later must not require editing old cards.

A spreader added two years from now should primarily mean:

1. add equipment definition;
2. give it tags;
3. author spreader cards.

Old generic cards continue using universal physical requirements such as:

```text
requires World.Ankles == Free
```

Do not create `HasSpreader`, `HasRope`, etc.

---

# 4. D8 ASSET REQUIREMENTS

The complete target requires these manually selected character poses:

1. `SexyVamp`
2. `LeaningForward`
3. `CrossedArmsAmused`
4. `Lounging`
5. `Main1`
6. `Sassy`

These are story aliases.

They are **not** DAZ filenames.

The user chooses the actual source pose for each alias.

Required one-shot gesture:

```text
Disapproval
```

This may use an existing prepared gesture if an appropriate one already exists.

Do not pretend a static pose is an animated one-shot gesture.

Required semantic locations:

```text
lara_start
player_start
camera_start
stepforward
Lounger
MainPosition1
Bedside
```

Required contact target:

```text
Penis
```

Required funscripts:

```text
Finisher1
FinishLoop
```

If actual authored finishing funscripts are not already present by D8, create clearly labeled engineering placeholders through the existing funscript import/runtime path.

Do not block the architectural sprint on final motion authoring.

---

# 5. AUDIO

The complete fixed script currently contains **19 fixed Lara dialogue beats**.

Use the supplied engineering placeholder package:

`Lara_D8_FullScene_Placeholder_Audio.zip`

These clips are temporary.

They exist only so engineering playback, SALSA, timing, transitions and phase integration can be tested without requiring a recording session.

The `.dazstory` file contains the authoritative transcript.

Audio files do not become the source of truth.

Parenthetical performance cues such as `(giggles)` may be absent from the synthetic placeholder voice.

Do not remove them from the authored transcript because the temporary TTS cannot perform them well.

---

# =====================================================================
# D5.5 — CORRECT THE AUTHORING AND EXECUTION FOUNDATION
# =====================================================================

# D5.5-01 — Freeze the full First Contact oracle

## Goal

Put the actual D8 target into the repository as a first-class product reference so future agents cannot work from incomplete remembered context.

## Add

Suggested:

`docs/story-runtime/FIRST_CONTACT_D8_ORACLE.md`

Copy the user's complete source script into it substantially verbatim.

Organize only enough to identify:

- Scene Setup
- Intro Card
- Warmup
- Main
- Climax
- Resolution

Do not rewrite it into implementation terminology.

Below it add a traceability table:

```text
Oracle line
Current implementation ticket
Status
Notes
```

Every meaningful command or behavior must eventually map to a ticket.

## Acceptance

There must be no D8 requirement that exists only in chat history.

---

# D5.5-02 — Replace human-authored story JSON with `.dazstory`

## Goal

Create the concise author-facing language.

## Extension

`.dazstory`

## Required language concepts

### Declarations

```text
setup FirstContact
{
}

card Intro
{
}

sequence FirstContact
{
}

phase Warmup
{
}
```

### Calls

```text
await Lara.Say("...")
Camera.Track(Lara)
Lara.PersistentPose(CrossedArmsAmused)
```

### Variables

```text
let repetitions = random(11..22)
```

### Assignment

```text
Session.IntensityMultiplier = 1.2
Session.Mood += 5
```

### Conditions

```text
if World.Clothing.Count == 0
{
}
else if World.Clothing.Count <= 2
{
}
else
{
}
```

### Compound predicates

```text
if World.Posture == Standing && World.Ankles == Free
```

Support:

```text
==
!=
<
<=
>
>=
&&
||
!
()
```

### Loops

```text
while Completion < 40%
{
}
```

and:

```text
repeat repetitions
{
}
```

### Durations

```text
500ms
1s
1.5s
```

### Lists

```text
[Warmup]
[Main, Standing]
```

### Named arguments

```text
await Director.DrawAndExecute(
    tags: [Main, Sitting],
    intensity: 1.2
)
```

Line wrapping is formatting only.

A normal simple command remains one line.

---

# D5.5-03 — Correct `await` semantics

The language must not assume every asynchronous operation is automatically blocking.

`await` means:

> start this command and do not advance this source thread until it completes.

Without `await`, an operation may start and continue concurrently **only when that verb's runtime contract permits it**.

Example:

```text
Lara.PersistentPose(CrossedArmsAmused)
await Lara.Say("Now let's look at your equipment...")
```

may allow Lara's pose blend to continue while speech begins.

The Story runtime owns every unawaited local operation:

- cancellation;
- failure reporting;
- cleanup;
- lifecycle.

It must not leak an unconsumed Unity `Awaitable`.

If an unawaited body operation conflicts with a later body operation, produce a clear runtime conflict rather than undefined animation ownership.

Persistent commands such as:

```text
Camera.Track(Lara)
```

return immediately by definition.

---

# D5.5-04 — Flat compiled program

## Goal

Remove the current recursive serializer limitation completely.

Pipeline:

```text
source text
    ↓
lexer
    ↓
parser
    ↓
AST
    ↓
semantic compiler
    ↓
flat instruction program
```

Do not store nested `StoryStep` objects.

Compiled instructions contain:

```text
opcode
typed operands
source file
source line
source column
jump destinations where required
```

Control flow is represented through instruction offsets/jumps.

Tests must successfully compile at least 32 nested source blocks.

There is no eight-level story-authoring limit.

---

# D5.5-05 — Import-time compilation and diagnostics

Implement a Unity importer for `.dazstory`.

A syntax error should look approximately like:

```text
FirstContact.dazstory:37:18
Unknown pose symbol 'SexyVmamp'
```

A binding error should identify:

- source file;
- declaration;
- line;
- expected binding type;
- missing symbol.

Do not require an author to locate opaque generated step IDs.

Generated internal IDs may exist for diagnostics but are not author-written ceremony.

---

# D5.5-06 — Symbolic content bindings

Preserve symbolic bindings.

The author writes:

```text
Lara.PersistentPose(SexyVamp)
```

not:

```text
Assets/Generated/DazPoses/whatever.asset
```

Binding types needed by D8:

```text
Location
Pose
Gesture
Audio/dialogue
MagicShaderProfile
Performer
PlayerBody
ContactTarget
Funscript
```

A binding knows its type.

Binding the wrong object is a validation error.

---

# D5.5-07 — Semantic location binding

The D8 script uses `Lounger` in multiple ways.

We should not force the author to remember:

```text
LoungerStandingMark
LoungerSeat
LoungerSeatAnchor
LoungerDissolveAnchor
```

when conceptually all are the Lounger.

Add a semantic story-location binding capable of providing:

```text
placement Transform
optional PerformerSeat
optional focus Transform
```

Thus:

```text
await Lara.DissolveTo(Lounger)
```

uses the placement.

```text
await Lara.SitAt(Lounger)
```

uses the seat.

Later:

```text
await Lara.DissolveTo(Lounger, seated: Lounging)
```

uses the seat placement and seated state.

This is domain-specific convenience justified directly by the authored scene.

Do not create a generalized scene-object dependency container.

---

# D5.5-08 — Dialogue remains readable

Author source:

```text
await Lara.Say("I'm Lara. I'm a digital hallucination.")
```

Compiler creates a deterministic dialogue ID from the text.

Keep:

```text
dialogue ID
transcript
AudioClip binding
source location
```

Changing the transcript invalidates the old audio binding.

No one should need to replace dialogue in source with:

```text
voice:fc-043
```

just to make playback work.

---

# D5.5-09 — Import all 19 placeholder clips

Import the supplied 19 WAV files into a clearly temporary location, e.g.:

```text
Assets/Production/Story/Audio/Placeholder/FirstContact/
```

Bind every fixed authored `Lara.Say` statement.

Missing placeholder speech is a validation failure for the D8 scene.

It is not acceptable to replace missing speech with text captions.

---

# D5.5-10 — `MagicShader`

Implement:

```text
await Lara.MagicShader(GlowyWireframe)
await Lara.MagicShader(Regular)
```

This belongs to Lara's performer/visual vocabulary.

It does **not** belong as renderer hacking inside `StoryRunner`.

Required modes now:

```text
Regular
GlowyWireframe
```

`GlowyWireframe` must visibly read in the Void environment as a glowing digital/wireframe projection.

`Regular` restores the exact active normal Lara material state.

Requirements:

- do not mutate shared source material assets;
- work with currently equipped Lara renderers;
- provide one refresh seam for wardrobe/hair changes;
- restoration must be deterministic;
- cancellation must leave Lara in a valid renderer state.

Do not build an enormous generalized shader scripting system.

---

# D5.5-11 — Story setup block

The source should be able to say:

```text
setup FirstContact
{
    Lara.StartHiddenAt(lara_start)
    PlayerBody.StartHiddenAt(player_start)
    Camera.StartAt(camera_start)
    Lara.StartIdle()
}
```

These commands establish the authored initial scene state before `sequence FirstContact`.

They map to existing prepared objects.

They do not rediscover rigs.

They do not reset saved tuning.

`StartIdle()` means use Lara's configured normal idle/initial performer state.

Do not invent a second idle animation controller.

---

# D5.5-12 — Migrate the exact Intro Card

The actual source must contain:

```text
card Intro
{
    await Lara.DissolveIn(lara_start)

    Camera.Track(Lara)

    await Lara.Say("Hi there. I was hoping you were brave enough to meet me.")

    await Lara.LocomoteTo(stepforward)

    await Lara.PersistentPose(SexyVamp)

    await Lara.Say("I'm Lara. I'm a digital hallucination.")

    await Lara.MagicShader(GlowyWireframe)

    wait 500ms

    await Lara.MagicShader(Regular)

    await Lara.Say("Well, more than a hallucination. A bit of a... projection. An intrusion? (giggles) I like that one. Intrusion.")

    await Lara.DissolveTo(Lounger)

    await Lara.Say("I'm the most fun you've ever had. But I come with... conditions.")

    await Lara.PersistentPose(LeaningForward)

    await Lara.Say("I can only give you pleasure if I have the power to do so. The more you give up to me, the more I can do for you. Let's see what you're giving up, tonight.")
}
```

No `SitAt`.

No camera move invented by the executor.

No dialogue omitted.

No negotiation inserted before the card ends.

---

# D5.5-13 — Migrate the complete negotiation/setup section

After:

```text
play Intro
```

the First Contact sequence continues with the actual negotiation.

Target authoring shape:

```text
await Popup.Show(Kinks)

await Lara.Say("Not bad for a start.")

SubmissionScore.Calculate()

await Lara.Say("This is your submission score. The more activities you consent to, the higher it goes, but... I don't want you bending past your real limits. Honesty is important. But...(giggles) so's letting me try new things. So next time we start a scene... look for things you're willing to try for me and let me know if you come up with any.")

Lara.PersistentPose(CrossedArmsAmused)

await Lara.Say("Now let's look at your equipment...(giggles) Not that equipment, yet.")

await Popup.Show(Equipment)

Session.SelectPreparedEquipment(1)

if Session.PreparedEquipment.Count > 0
{
    await Popup.ShowNeededEquipment(Session.PreparedEquipment)
}

await Lara.Say("And finally, tell me what you're wearing....")

await Popup.Show(Clothing)
```

Use the existing submission calculation exactly.

Kink and equipment edits persist exactly as D1–D5 already established.

---

# D5.5-14 — Exact clothing branches

Use the already-established ordering:

```text
Shoes
Socks
Shirt
Pants
Bra
Underwear
```

### Nothing worn

```text
if World.Clothing.Count == 0
{
    await Lara.Say("Now that's a go-getter. The less you wear to our sessions, the better.")
    Session.Mood += 5
}
```

Mood began at 50.

Result is 55.

Do not create another happiness property.

---

### One or two

```text
else if World.Clothing.Count <= 2
{
    await Lara.Say("I like you naked, but I can wait to get you there...")
}
```

No mood change.

---

### More than two

```text
else
{
    Lara.PlayOneShotGesture(Disapproval)

    await Lara.Say("That's way too dressed. The fewer you wear the happier I am. In fact... take one off now.")

    let item = World.Clothing.LeastRisqueWorn()

    await Popup.RemoveClothingItem(item)

    World.Clothing.MarkRemoved(item)
}
```

`LeastRisqueWorn()` uses exactly:

1. Shoes
2. Socks
3. Shirt
4. Pants
5. Bra
6. Underwear

The removal popup names the item and waits for Ready.

There is no computer-vision verification.

No mood penalty is invented.

---

# D5.5-15 — MANUAL CONTENT CHECKPOINT

Do not stop the executor at the beginning of D5.5.

First implement and compile the infrastructure above.

Then stop for the user's authored pose selections.

The executor reports:

```text
READY FOR FIRST CONTACT CONTENT IMPORT

Required pose aliases:
- SexyVamp
- LeaningForward
- CrossedArmsAmused
- Lounging
- Main1
- Sassy
```

## User workflow

For each alias:

1. Open DazPoseTool.
2. Find a DAZ pose appropriate to that story purpose.
3. Import as **Pose**, not Expression.
4. Let the existing bridge create the canonical pose data.
5. Let Unity generate:
   - `.anim`;
   - `PerformerPose` wrapper.
6. Inspect the result on Lara.
7. Bind the generated **PerformerPose wrapper**, not the raw `.anim`, to its story alias.

### Pose guidance

`SexyVamp`
- standing;
- seductive/confident;
- good player-facing silhouette.

`LeaningForward`
- intimate/control-oriented;
- appropriate near the Lounger.

`CrossedArmsAmused`
- standing;
- amused/skeptical;
- visually works during equipment conversation.

`Lounging`
- genuinely seated/lounging;
- suitable for the Lounger seat anchors.

`Main1`
- standing;
- authoritative main-phase stance.

`Sassy`
- standing;
- playful/commanding resolution pose.

The pose binding records expected universal posture:

```text
SexyVamp           Standing
LeaningForward     Standing
CrossedArmsAmused  Standing
Lounging            Sitting
Main1              Standing
Sassy              Standing
```

Applying a story pose updates `World.Posture` from this authored metadata.

Do not infer posture from animation bone geometry.

---

# D5.5-16 — Disapproval gesture

If a suitable existing `PerformerGesture` exists, bind it to:

```text
Disapproval
```

If none exists, create/use an explicitly labeled temporary gesture fixture.

Do not silently replace the gesture with an expression.

The source call:

```text
Lara.PlayOneShotGesture(Disapproval)
```

starts the gesture and allows compatible speech to continue concurrently.

---

# D5.5 ACCEPTANCE

D5.5 is accepted when:

- `.dazstory` is the preferred story authoring surface;
- old JSON remains legacy-compatible but is no longer active First Contact source;
- flat compiled programs replace recursive authoring DTOs;
- exact Intro executes;
- exact negotiation executes;
- all 19 fixed lines are represented and bound;
- six pose aliases exist;
- clothing branches match the oracle;
- `MagicShader` works visibly;
- First Contact reaches the Warmup boundary.

---

# =====================================================================
# D6 — CARDS AS AUTHORED PERFORMANCE BEATS
# =====================================================================

# D6-01 — CardDefinition domain model

Create storage-independent:

```text
CardDefinition
```

Minimum fields:

```text
Id
Tags
Requirements
BaseWeight
Progress
MaxUsesPerSession
MinCardsBetweenUses
ProgramEntry
```

Do not put Unity objects here.

`ProgramEntry` identifies a compiled card body.

---

# D6-02 — Concise card metadata authoring

A `.dazstory` card should remain readable:

```text
card WarmupExample
tags [Warmup]
progress 10
weight 1
repeat unlimited
cooldown 2
requires World.Hands == Free
requires World.Ankles == Free
{
    ...
}
```

For a one-use card:

```text
repeat once
```

Internally this may compile to:

```text
MaxUsesPerSession = 1
```

Do not make authors write that implementation field unless a more advanced editor needs it.

---

# D6-03 — Tags are classification, state is state

These are distinct.

Example:

```text
card JumpingWarmup
tags [Warmup, Standing, Energetic]
requires World.Posture == Standing
requires World.Ankles == Free
{
}
```

`Standing` in `tags` means:

> this card belongs to the Standing content pool.

`World.Posture == Standing` means:

> this card is physically legal right now.

Do not make either replace the other.

---

# D6-04 — Equipment tag eligibility

The current equipment catalog already exposes identity/theme tags.

Cards can use them.

Example:

```text
card RopeExample
tags [Main, equipment:rope]
requires World.Hands == Free
{
}
```

Adding `equipment:spreader` later does not modify old cards.

Equipment-specific content only needs appropriate new tags/cards.

---

# D6-05 — Runtime card requirements

Requirements are hard eligibility rules.

If false, the card cannot be selected.

Support:

```text
World state
Session state
Contract/profile snapshot facts
active equipment tags
prepared equipment facts where explicitly requested
```

Do not use arbitrary reflection.

Provide clear rejection reasons:

```text
Rejected JumpingWarmup:
World.Ankles == Restricted
required Free
```

---

# D6-06 — Card execution context

Every card invocation receives:

```text
CardExecutionContext
```

including:

```text
CardId
SessionSeed/RNG
IntensityMultiplier
Phase
InvocationNumber
prepared equipment
active equipment
```

The card can access concise built-ins such as:

```text
Card.Intensity
Card.Invocation
```

---

# D6-07 — Intensity semantics

The user's:

```text
Executor.BonusIntensityModifier = 1.2
```

becomes:

```text
Session.IntensityMultiplier = 1.2
```

Card execution receives that multiplier.

Do **not** magically make everything 20% faster.

Intensity is input to authored card behavior.

Example:

```text
let repetitions = round(10 * Card.Intensity)
```

or:

```text
Toy.Vibrate(0.5 * Card.Intensity)
```

or any future card-specific interpretation.

The engine provides the number.

The card defines what intensity means for its performance.

This avoids nonsensical automatic scaling of dialogue, walking speed, dissolve durations, etc.

---

# D6-08 — Card-local randomness

Support:

```text
let repetitions = random(11..22)
```

using the session RNG.

Do not use `UnityEngine.Random`.

Same session seed + same execution path gives reproducible choices.

Card-local randomness is separate from Director card-selection randomness but uses the same owned session random service.

---

# D6-09 — Progress contribution

Every selected card has authored base progress.

Example:

```text
progress 10
```

On successful completion:

```text
actualProgress =
    card.Progress
    * SessionContract.PacingMultiplier
```

Then:

```text
Session.PhaseCompletion += actualProgress
```

Example:

```text
card progress = 10
pacing = 0.33

actual ≈ 3.3
```

A slow session therefore naturally selects roughly three times as many cards.

Do not multiply:

- mood changes;
- state effects;
- intensity;
- equipment effects.

Pacing scales phase progress only.

---

# D6-10 — Repeat history

Track per session:

```text
times used
last selection index
recent card IDs
```

Eligibility:

```text
MaxUsesPerSession
```

Weighting:

```text
MinCardsBetweenUses
```

A card on cooldown is rejected with a reason.

A repeatable card remains legal once its cooldown is satisfied.

---

# D6-11 — Card outcomes/state changes

Card bodies may use ordinary story statements:

```text
World.Ankles = Restricted
Equipment.Activate(Rope)
Session.Mood += 3
```

`Equipment.Activate(...)` adds the equipment's tags to active-session equipment.

It does **not** automatically decide universal physical consequences.

A rope card that ties ankles explicitly changes:

```text
World.Ankles = Restricted
```

A different rope card need not.

This preserves the hybrid model.

---

# D6-12 — Minimum D8 card library

D8 cannot claim success with empty card pools.

Author a small engineering/demo library sufficient to exercise the Director:

### Warmup

At least 3 cards tagged:

```text
Warmup
```

with meaningful differences and enough repeatability to reach 100%.

### Main Standing

At least 3:

```text
Main
Standing
```

### Main Sitting

At least 3:

```text
Main
Sitting
```

### Resolution

At least 2:

```text
Resolution
```

### Equipment

The five current representative equipment types are:

```text
butt-plug
rope
dildo-anal
dildo-oral
ice
```

Each must have at least one engineering card matching its stable identity tag so **any item the setup randomly selects can be executed later**.

Do not fix the problem by secretly restricting equipment selection to the one equipment item for which an executor happened to author a card.

That would violate the authored behavior.

The demo cards may be simple.

Their job is to validate architecture, not constitute the final content library.

---

# D6-13 — Card validation

Before Play, detect:

- duplicate card IDs;
- unknown tags where a controlled tag is required;
- malformed predicates;
- unresolved program entry;
- impossible numeric metadata;
- missing equipment coverage for the First Contact prepared-equipment pool.

For First Contact specifically, preflight should prove:

> every equipment item eligible for `SelectPreparedEquipment(1)` has at least one matching D8 equipment card.

---

# D6 ACCEPTANCE

D6 is accepted when a standalone test can:

1. load a card catalog;
2. inspect candidate cards;
3. reject physically illegal cards;
4. choose a card;
5. execute the compiled card body;
6. apply its progress;
7. apply its state changes;
8. honor repeatability/cooldown;
9. reproduce choices with a fixed seed.

---

# =====================================================================
# D7 — DIRECTOR, PHASES, SELECTION AND SESSION PACING
# =====================================================================

# D7-01 — Phase runtime

A phase has:

```text
Name
Completion
```

Entering:

```text
phase Warmup
{
}
```

must:

```text
Session.Phase = Warmup
Session.PhaseCompletion = 0
```

Leaving the phase does not erase session history.

---

# D7-02 — Director selection request

Create a storage-independent:

```text
CardSelectionRequest
```

Minimum D8 query capabilities:

```text
required tags
optional selected-equipment tags
optional explicit Card ID
intensity
```

Example:

```text
Director.DrawAndExecute(tags: [Warmup])
```

Example:

```text
Director.DrawAndExecute(tags: [Main, Standing])
```

Example exact-card form:

```text
Director.Execute(CardName)
```

This preserves the user's requirement that phases can either:

- draw matching content;
- explicitly play THIS card.

---

# D7-03 — Selection pipeline

For every draw:

```text
1. gather catalog
2. match requested tags
3. check hard requirements
4. check repeatability/cooldown
5. calculate weights
6. seeded weighted selection
7. execute
8. record history
9. apply successful progress
10. return result
```

A card that fails during execution does not receive completion credit.

---

# D7-04 — Weighting

D8 needs:

```text
BaseWeight
recency reduction
```

No elaborate personality AI is required yet.

Do not claim the full future Lara decision AI has been solved.

But selection architecture must be able to accept additional score contributors later such as:

```text
intent
mood suitability
favor
history
player reaction
```

without replacing CardDefinition or StoryRunner.

---

# D7-05 — Candidate explanation/debugger

F8 Story debug should show at least:

```text
Session seed
Current phase
Completion
Pacing multiplier
Mood
Intensity multiplier
Prepared equipment
Active equipment
World posture
Hands
Ankles
Occupancies
Clothing
Recent card history

Last Director query
All considered cards
    eligible/rejected
    rejection reason
    final weight
Selected card
```

This is not optional developer fluff.

The Director will be difficult to tune without seeing why it chose something.

---

# D7-06 — Prepared-equipment card draw

First Contact selects one prepared item during setup.

Later:

```text
await Director.DrawAndExecute(
    equipment: Session.PreparedEquipment
)
```

For D8 there is only one item.

Matching semantics:

The candidate card must match at least one identity/content tag exposed by that selected equipment item.

Prefer its most specific:

```text
equipment:<id>
```

tag where present.

Generic equipment tags may later support shared content.

If `PreparedEquipment` is empty:

- skip this equipment beat successfully;
- do not invent equipment;
- log that the optional beat was skipped.

---

# D7-07 — Warmup source

The actual authored flow should now read approximately:

```text
phase Warmup
{
    await Lara.SitAt(Lounger)

    await Lara.PersistentSitPose(Lounging)

    await Lara.Say("Alright, let's get you warmed up.")

    while Completion < 40%
    {
        await Director.DrawAndExecute(tags: [Warmup])
    }

    if World.Clothing.Count > 0
    {
        await Lara.Say("I think it's time to lose the rest of the clothes... do that and tell me when you're back.")

        await Popup.Ready("Ready")

        World.Clothing.Clear()
    }

    while Completion < 100%
    {
        await Director.DrawAndExecute(tags: [Warmup])
    }
}
```

No fixed authored card sequence.

Director chooses cards.

---

# D7-08 — Persistent seated pose

Implement:

```text
await Lara.PersistentSitPose(Lounging)
```

This maintains the existing seat anchoring/ownership semantics.

Do not simply play an arbitrary standing `PoseAsync` and let Lara drift away from the chair.

The performer seating system owns root placement.

The authored `Lounging` pose controls the body presentation while seated.

`World.Posture` remains:

```text
Sitting
```

---

# D7-09 — Main phase through the equipment beat

Author:

```text
phase Main
{
    await Lara.DissolveTo(MainPosition1)

    await Lara.PersistentPose(Main1)

    await Lara.Say("That was all the warmup I'm going to give you. Now let's get down to business.")

    while Completion < 30%
    {
        await Director.DrawAndExecute(tags: [Main, Standing])
    }

    await Lara.DissolveTo(Lounger, seated: Lounging)

    if Session.PreparedEquipment.Count > 0
    {
        await Director.DrawAndExecute(
            equipment: Session.PreparedEquipment
        )
    }

    Session.IntensityMultiplier = 1.2

    while Completion < 60%
    {
        await Director.DrawAndExecute(
            tags: [Main, Sitting]
        )
    }

    Session.IntensityMultiplier = 1.4

    while Completion < 100%
    {
        await Director.DrawAndExecute(
            tags: [Main, Sitting]
        )
    }
}
```

---

# D7-10 — New dissolve-to-seated behavior

The authored requirement:

> Lara.DissolveTo(Lounger, in sit position - new feature!)

becomes:

```text
await Lara.DissolveTo(Lounger, seated: Lounging)
```

This is one coherent performer operation.

Behavior:

1. current Lara dissolves out;
2. performer resolves Lounger seat/seat anchor;
3. Lara root is placed correctly;
4. seating ownership/state is established;
5. `Lounging` seated pose is established;
6. Lara dissolves back in;
7. world posture becomes Sitting;
8. command completes.

Do not:

- visibly walk to the seat;
- briefly appear standing on the seat;
- call `SitAt` after she is already visible;
- leave world state saying Standing.

This feature belongs to performer/seating vocabulary, not StoryRunner renderer manipulation.

---

# D7-11 — Phase deadlock protection

A malformed content library must not spin forever.

Before entering a completion loop, Director must detect:

> no eligible cards capable of satisfying this query.

Fail with a clear error:

```text
Warmup cannot advance:
0 eligible cards for tags [Warmup]
Completion 37/40
```

Also provide a high but reasonable runtime selection-count guard against pathological zero-progress card loops.

A `progress 0` card may exist, but a pool containing only zero-progress cards cannot satisfy a completion loop.

---

# D7 ACCEPTANCE

A seeded test run must complete:

```text
Warmup 0 → 40 → clothing transition → 100
Main 0 → 30 → equipment beat → 60 → 100
```

using drawn cards rather than hardcoded card names.

---

# =====================================================================
# D8 — COMPLETE FIRST CONTACT SESSION
# =====================================================================

# D8-01 — Saved scene conformance

Create/finalize the actual First Contact production scene in the existing testing Void.

Scene must contain/configure:

```text
Lara
Player Body
camera
Game Session / host
story runner
story bindings
Director
card catalog/provider
prompt UI
climax action UI
motion driver
semantic locations
```

Initial setup must conform to:

```text
Lara hidden at lara_start
PlayerBody hidden at player_start
Camera at camera_start
Lara idle
```

Do not retune or rebuild the Void art.

---

# D8-02 — Full sequence source

The final `FirstContact.dazstory` must contain the complete readable sequence.

Conceptually:

```text
sequence FirstContact
{
    play Intro

    // complete negotiation/setup

    phase Warmup
    {
        ...
    }

    phase Main
    {
        ...
    }

    phase Climax
    {
        ...
    }

    phase Resolution
    {
        ...
    }
}
```

There must not be a hidden C# class containing half of the scene while the `.dazstory` pretends to be the authoring source.

Infrastructure lives in C#.

Scene choreography lives in authored source.

---

# D8-03 — Climax phase setup

Author:

```text
phase Climax
{
    await PlayerBody.DissolveIn()

    await Lara.DissolveTo(Bedside)

    await Lara.Say("I'm amused enough, I suppose. And since this is your first time, I'm not going to frustrate you... or even make you beg. I'm just going to make you cum.")

    await Lara.Grip(Penis)

    MotionDriver.PlayFunscript(Finisher1)

    ...
}
```

`Penis` resolves through the prepared player body's existing measured contact setup.

No rig discovery in story source.

---

# D8-04 — Climax player action

When Climax begins, expose a persistent player action:

```text
I came
```

Authoring surface:

```text
UI.ShowAction(ICame, "I came")
```

This is not a fullscreen blocking popup.

It is an in-session action the player may press while the motion program runs.

On press, emit:

```text
ICame
```

exactly once for that active phase invocation.

Disable/remove it when leaving Climax.

---

# D8-05 — Await first of multiple events

The story language now needs a real race primitive.

Use readable form:

```text
let climaxResult = await wait any
{
    MotionDriver.Ended
    UI.Pressed(ICame)
}
```

Semantics:

- subscribe to all listed events;
- first event wins;
- unsubscribe the losers;
- return which event won;
- cancellation cancels the wait;
- no per-frame story polling;
- events from a previous invocation cannot satisfy a new wait.

---

# D8-06 — Motion-driver completion event

A non-looping funscript emits:

```text
MotionDriver.Ended
```

once when it naturally reaches the end.

Stopping/cancelling playback does not masquerade as natural completion.

A looping funscript does not emit Ended every loop.

---

# D8-07 — Complete climax logic

Exact authored behavior:

```text
UI.ShowAction(ICame, "I came")

MotionDriver.PlayFunscript(Finisher1)

let climaxResult = await wait any
{
    MotionDriver.Ended
    UI.Pressed(ICame)
}

if climaxResult == MotionDriver.Ended
{
    MotionDriver.PlayFunscript(FinishLoop, loop: true)

    await UI.WaitFor(ICame)
}

MotionDriver.Stop()

UI.HideAction(ICame)
```

If `ICame` wins during `Finisher1`, advance immediately.

If `Finisher1` ends first, run `FinishLoop` continuously until `ICame`.

Do not require the player to wait for the current loop to finish after pressing.

---

# D8-08 — Resolution phase

Author:

```text
phase Resolution
{
    await Lara.Say("*giggles* Well, you seem to understand the assignment. Some times you're going to have to work for it. Some times you may have to work and not even get it then. It all depends on my mood, and how much you decide to trust me.")

    await Lara.DissolveTo(lara_start)

    await Lara.PersistentPose(Sassy)

    await Lara.Say("Now, how am I going to keep you thinking about me until our next session?")

    await Director.DrawAndExecute(tags: [Resolution])

    await Lara.Say("Alright, that's our session for tonight. Until next time... behave. Or don't. I get off either way. *giggles* Bye!")
}
```

A Resolution card is genuinely drawn.

Do not replace it with a hard-coded placeholder command in the scene sequence.

---

# D8-09 — Resolution card seam for future assignments

D8 does not need to finish the full persistent assignment system unless a Resolution demo card uses one.

However the Resolution draw must execute through normal cards.

Do not create a special `ResolutionCardRunner`.

A future assignment-issuing Resolution card must be able to use the same normal card body/state services.

---

# D8-10 — Complete 19-line dialogue conformance

Automated/source validation must prove the complete fixed dialogue set exists.

Expected fixed speech count:

```text
19
```

Card-generated speech is additional and does not count toward this fixture.

Test transcript hashes, not WAV filenames.

This prevents a future audio rename from breaking the oracle check.

---

# D8-11 — Full oracle trace test

Create a deterministic integration trace using fixed:

```text
session seed
kink selections
equipment ownership
clothing selections
I Came timing
```

Run multiple scenarios.

Minimum:

### A — Naked, equipment available, player comes before Finisher1 ends

Must exercise:

```text
mood +5
prepared equipment popup
Warmup
Main equipment card
early ICame path
Resolution
```

### B — 1–2 clothing items, no equipment

Must exercise:

```text
neutral clothing response
no prepared-equipment popup
equipment card beat skipped
normal phases
Finisher1 may end first
FinishLoop
ICame
Resolution
```

### C — >2 clothing items

Must exercise:

```text
Disapproval gesture
least-risque removal
state update
remaining-clothes Warmup removal at 40%
```

### D — physically illegal card exists

Put an ineligible card in the pool and verify it is rejected rather than executed.

---

# D8-12 — Full source-to-runtime conformance test

The integration test must prove that the actual authored source reaches runtime in this semantic order:

```text
setup
Intro
kinks
submission calculation
equipment
prepared equipment
clothing branch
Warmup 0→40
clothing cleanup
Warmup 40→100
Main position
Main standing cards 0→30
dissolve-to-seated
equipment card
Main sitting 30→60 @ 1.2
Main sitting 60→100 @ 1.4
PlayerBody reveal
Bedside
climax dialogue
Grip
Finisher1
ICame/Ended race
FinishLoop if necessary
Resolution dialogue
return to start
Sassy
Resolution card
goodbye
complete
```

No implementation may report D8 complete while skipping one of these because the rest "demonstrates the concept."

---

# D8-13 — Human manual acceptance

Run the actual saved scene.

The user should specifically inspect:

## Setup

- Lara hidden and correctly positioned;
- male player body hidden;
- camera at intended mark;
- correct Void environment;
- Lara enters from expected position.

## Intro

- camera tracking;
- locomotion;
- each selected pose;
- wireframe effect;
- 500ms visual hold;
- regular-material restoration;
- dissolve to Lounger;
- complete dialogue.

## Setup UI

- Kinks fullscreen;
- score behavior;
- Equipment fullscreen;
- Needed for Session popup;
- Clothing dialog;
- all three clothing response branches.

## Warmup

- correct seated presentation;
- cards genuinely vary;
- 40% boundary;
- clothing prompt;
- continuation to 100%.

## Main

- correct MainPosition1;
- Standing card pool;
- dissolve directly into seated Lounger state;
- equipment-matched card;
- visible/useful intensity difference where authored by demo cards;
- Sitting pools.

## Climax

- player-body reveal;
- Bedside relocation;
- hand contact;
- Finisher1;
- I Came button;
- correct early-completion behavior;
- correct FinishLoop fallback.

## Resolution

- dialogue;
- return to start;
- Sassy pose;
- Resolution card;
- goodbye.

Functional acceptance and aesthetic approval are logged separately.

A pose can be technically functional but aesthetically rejected.

A placeholder voice can sound terrible and still pass functional audio plumbing.

---

# D8-14 — Debug evidence

At completion, provide an execution report containing:

```text
starting SHA
ending SHA
all commits
changed files

story parser/compiler tests
card tests
Director tests
phase tests
climax event-race tests
full D8 trace tests

active FirstContact.dazstory path
card catalog path/provider
19 dialogue bindings
six pose bindings
Disapproval binding
GlowyWireframe binding
semantic location bindings
Finisher1 path
FinishLoop path

fixed test seed(s)
selected card history for test scenarios
rejected-card reasons
phase-completion traces
final session state
```

---

# D8-15 — Documentation/handoff

Update author documentation around the thing we are **actually building**.

A fresh content author should learn:

### Scene choreography

```text
.dazstory
```

### Card

```text
card Name
tags [...]
progress ...
requires ...
{
    performance commands
}
```

### Phase

```text
phase Warmup
{
    while Completion < 100%
        await Director.DrawAndExecute(...)
}
```

### Randomized local behavior

```text
let x = random(11..22)
```

### Explicit card

```text
await Director.Execute(MySpecificCard)
```

### Draw from pool

```text
await Director.DrawAndExecute(tags: [Warmup])
```

Do not make the verbose compiled representation the tutorial.

---

# =====================================================================
# NORTH-STAR FIRST CONTACT SOURCE
# =====================================================================

By D8, the active source should look broadly like this.

Exact syntax may change only where the parser requires punctuation. Do not change the conceptual shape.

```text
setup FirstContact
{
    Lara.StartHiddenAt(lara_start)
    PlayerBody.StartHiddenAt(player_start)
    Camera.StartAt(camera_start)
    Lara.StartIdle()
}


card Intro
{
    await Lara.DissolveIn(lara_start)

    Camera.Track(Lara)

    await Lara.Say("Hi there. I was hoping you were brave enough to meet me.")

    await Lara.LocomoteTo(stepforward)

    await Lara.PersistentPose(SexyVamp)

    await Lara.Say("I'm Lara. I'm a digital hallucination.")

    await Lara.MagicShader(GlowyWireframe)

    wait 500ms

    await Lara.MagicShader(Regular)

    await Lara.Say("Well, more than a hallucination. A bit of a... projection. An intrusion? (giggles) I like that one. Intrusion.")

    await Lara.DissolveTo(Lounger)

    await Lara.Say("I'm the most fun you've ever had. But I come with... conditions.")

    await Lara.PersistentPose(LeaningForward)

    await Lara.Say("I can only give you pleasure if I have the power to do so. The more you give up to me, the more I can do for you. Let's see what you're giving up, tonight.")
}


sequence FirstContact
{
    play Intro


    await Popup.Show(Kinks)

    await Lara.Say("Not bad for a start.")

    SubmissionScore.Calculate()

    await Lara.Say("This is your submission score. The more activities you consent to, the higher it goes, but... I don't want you bending past your real limits. Honesty is important. But...(giggles) so's letting me try new things. So next time we start a scene... look for things you're willing to try for me and let me know if you come up with any.")

    Lara.PersistentPose(CrossedArmsAmused)

    await Lara.Say("Now let's look at your equipment...(giggles) Not that equipment, yet.")

    await Popup.Show(Equipment)

    Session.SelectPreparedEquipment(1)

    if Session.PreparedEquipment.Count > 0
    {
        await Popup.ShowNeededEquipment(Session.PreparedEquipment)
    }

    await Lara.Say("And finally, tell me what you're wearing....")

    await Popup.Show(Clothing)


    if World.Clothing.Count == 0
    {
        await Lara.Say("Now that's a go-getter. The less you wear to our sessions, the better.")

        Session.Mood += 5
    }
    else if World.Clothing.Count <= 2
    {
        await Lara.Say("I like you naked, but I can wait to get you there...")
    }
    else
    {
        Lara.PlayOneShotGesture(Disapproval)

        await Lara.Say("That's way too dressed. The fewer you wear the happier I am. In fact... take one off now.")

        let item = World.Clothing.LeastRisqueWorn()

        await Popup.RemoveClothingItem(item)

        World.Clothing.MarkRemoved(item)
    }


    phase Warmup
    {
        await Lara.SitAt(Lounger)

        await Lara.PersistentSitPose(Lounging)

        await Lara.Say("Alright, let's get you warmed up.")

        while Completion < 40%
        {
            await Director.DrawAndExecute(tags: [Warmup])
        }

        if World.Clothing.Count > 0
        {
            await Lara.Say("I think it's time to lose the rest of the clothes... do that and tell me when you're back.")

            await Popup.Ready("Ready")

            World.Clothing.Clear()
        }

        while Completion < 100%
        {
            await Director.DrawAndExecute(tags: [Warmup])
        }
    }


    phase Main
    {
        await Lara.DissolveTo(MainPosition1)

        await Lara.PersistentPose(Main1)

        await Lara.Say("That was all the warmup I'm going to give you. Now let's get down to business.")

        while Completion < 30%
        {
            await Director.DrawAndExecute(tags: [Main, Standing])
        }

        await Lara.DissolveTo(Lounger, seated: Lounging)

        if Session.PreparedEquipment.Count > 0
        {
            await Director.DrawAndExecute(
                equipment: Session.PreparedEquipment
            )
        }

        Session.IntensityMultiplier = 1.2

        while Completion < 60%
        {
            await Director.DrawAndExecute(tags: [Main, Sitting])
        }

        Session.IntensityMultiplier = 1.4

        while Completion < 100%
        {
            await Director.DrawAndExecute(tags: [Main, Sitting])
        }
    }


    phase Climax
    {
        await PlayerBody.DissolveIn()

        await Lara.DissolveTo(Bedside)

        await Lara.Say("I'm amused enough, I suppose. And since this is your first time, I'm not going to frustrate you... or even make you beg. I'm just going to make you cum.")

        await Lara.Grip(Penis)

        UI.ShowAction(ICame, "I came")

        MotionDriver.PlayFunscript(Finisher1)

        let climaxResult = await wait any
        {
            MotionDriver.Ended
            UI.Pressed(ICame)
        }

        if climaxResult == MotionDriver.Ended
        {
            MotionDriver.PlayFunscript(FinishLoop, loop: true)

            await UI.WaitFor(ICame)
        }

        MotionDriver.Stop()

        UI.HideAction(ICame)
    }


    phase Resolution
    {
        await Lara.Say("*giggles* Well, you seem to understand the assignment. Some times you're going to have to work for it. Some times you may have to work and not even get it then. It all depends on my mood, and how much you decide to trust me.")

        await Lara.DissolveTo(lara_start)

        await Lara.PersistentPose(Sassy)

        await Lara.Say("Now, how am I going to keep you thinking about me until our next session?")

        await Director.DrawAndExecute(tags: [Resolution])

        await Lara.Say("Alright, that's our session for tonight. Until next time... behave. Or don't. I get off either way. *giggles* Bye!")
    }
}
```

That source file itself is a D8 acceptance artifact.

If the implementation is complete but the actual authored source has become substantially less readable than this, stop and fix the authoring layer.

---

# DEPENDENCY ORDER

Execute in this order:

```text
D5.5-01  Oracle
    ↓
D5.5-02..11  language/runtime correction
    ↓
D5.5-12..14 exact Intro/setup migration
    ↓
D5.5-15     USER POSE CHECKPOINT
    ↓
D5.5-16     gesture/content binding
    ↓
D6          cards
    ↓
D7          Director/phases
    ↓
D8-01..02   complete saved scene/source
    ↓
D8-03..07   Climax
    ↓
D8-08       Resolution
    ↓
D8-10..14   complete conformance/acceptance
```

Do not begin Director implementation against the old JSON authoring representation.

---

# HARD EXECUTOR RULES

1. Do not substitute a different behavior merely because it is already implemented.

2. Do not convert `DissolveTo` into `SitAt`.

3. Do not omit dialogue because audio is temporary.

4. Do not add UI steps simply because they are convenient for implementation.

5. Do not change `submissionScore`.

6. Do not replace equipment tags with equipment-specific state fields.

7. Do not replace universal physical state with an uncontrolled tag soup.

8. Do not hardcode card sequences where the oracle calls for Director draws.

9. Do not draw random cards where the oracle requests an explicit card.

10. Do not make card selection nondeterministic outside the session-owned RNG.

11. Do not count failed cards as phase progress.

12. Do not let impossible card pools spin indefinitely.

13. Do not hide half the choreography in a C# integration class.

14. Do not require ordinary authors to write GUIDs, hierarchy paths, raw asset paths or generated step IDs.

15. Do not make the current third-party/editor choice part of the domain model.

16. Do not claim D8 from source inspection alone.

17. The actual saved scene must run.

---

# FINAL DEFINITION OF D8

D8 is complete when the user's complete First Contact script has become a real playable session, and the human-authored source still looks recognizably like the script that designed it.

Not:

> "the architecture could theoretically support it."

Not:

> "representative parts are implemented."

Not:

> "all individual APIs exist."

The actual scene must execute:

```text
Setup
Intro
Negotiation
Warmup
Main
Climax
Resolution
```

in full.