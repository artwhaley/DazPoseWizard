# T05 — Story Events and Reusable Prompt Service

**Roadmap mapping:** D2  
**Depends on:** T04

## Goal

Provide awaitable user/story events and reusable fullscreen prompt surfaces without putting UI logic into the story runner.

## Add

Under `Story/Runtime/`:
- `StoryEventHub.cs`
- `StoryEvent.cs`

Under `Story/UI/`:
- `StoryPromptService.cs`
- `StoryPromptResult.cs`
- presenters/panels needed by T09/T10

### Event hub

Per session/run, not static global.

Required operations:
- `Emit(string eventId, string value = null)`
- `Awaitable<StoryEvent> WaitAsync(string eventId, CancellationToken-equivalent/owned run cancellation seam)`

Use the project's Unity `Awaitable` conventions. Do not introduce Task-only parallelism unless required by an existing API.

Waiting must cancel when the run/session stops.

No queued event history beyond what is necessary for a currently awaited interaction. This is not an event-sourcing system.

### `event.wait`
Wait until matching event and continue.

### `event.emit`
Emit immediately.

### Prompt service

Story runner calls service methods, never reaches buttons directly.

Required methods:
- `ShowMessageAsync(title, body, confirmLabel)`
- `ShowKinkEditorAsync(...)`
- `ShowEquipmentEditorAsync(...)`
- `ShowClothingEditorAsync(...)`

Only one modal story prompt is active at a time.
Opening a second prompt while one is active is an explicit programming error.

Stop cancels/ closes the prompt.

## Presentation

Use the project's existing UI visual language/components where reasonable.
Do not spend this sprint inventing a final art system.
Panels must be readable and functional.

## Acceptance

UI can emit a completion event/result to the Story runner without the runner knowing concrete Button/GameObject references.
