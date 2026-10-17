# T06 — Player Body Presence and Dissolve Vocabulary

**Roadmap mapping:** D3  
**Depends on:** T04

## Goal

Add player-body appear/disappear behavior using Lara's established dissolve visual language.

## Add

Under `Assets/Player/Runtime/` or the existing player runtime assembly:
- `PlayerBodyPresence.cs`
- `PlayerBodyVisibilityState.cs`

Extend the scoped player authoring facade (locate its current file under DazPose Authoring) with:
- `Awaitable DissolveBodyInAsync(float seconds = <same sensible dissolve default>)`
- `Awaitable DissolveBodyOutAsync(float seconds = <same sensible dissolve default>)`
- read-only body visibility state if useful.

Do not expose raw renderer mutation through the story runtime.

## Visual implementation

First inspect Lara's current dissolve implementation.

Preferred order:
1. Reuse an existing generic dissolve/effect component if it already supports arbitrary renderer sets.
2. If Lara's implementation is tightly embedded, extract only the smallest reusable renderer-dissolve driver needed by both Lara and player body.
3. Do not rewrite the entire Lara effect pipeline.

The player body effect must use the same particle/material visual family as Lara unless the existing implementation structurally cannot be shared; if so, preserve visual settings via serialized references rather than approximating new values.

## State/lifecycle

- Player body can start Hidden in the saved First Contact scene.
- In/Out calls are finite and awaitable.
- Stop cancels transition consistently with existing performance cancellation semantics.
- Last committed visible/hidden state is readable.
- No automatic lifecycle reset is added.
- Do not add `DissolveTo` for player body in this packet; First Contact only needs in/out.

## Story opcodes

Wire:
- `player.body.dissolveIn`
- `player.body.dissolveOut`

through the scoped player facade.

## Acceptance

Source path preserves the rule: Story → `PerformanceRun.Player` scoped facade → player body implementation.
No Story code manipulates renderers directly.
