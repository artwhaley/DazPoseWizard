# U03 — Profile draft/Apply/Cancel contract

**Depends:** U02. **Goal:** Edits cannot accidentally commit in session, filter or Cancel.

Create domain `PlayerProfileEditSession` with deep structural copy preserving all persisted fields and unknown IDs; isolated mutations for `KinkPreference` and `EquipmentPreference`; `Commit()` validates/commits once using existing `PlayerProfileService.Commit` and existing submission score calculation; `Discard()` no mutation. In-session interest apply refreshes session contract kink snapshot **after** successful commit; equipment apply affects profile and prepared selection only at its authored later point. Session clothing editor creates a separate transient World draft; Apply writes once, Cancel/Stop none. No custom JSON clone dropping fields. Draft remains stable through search/sort/filter and reopening modal. Esc, outside click (if supported) and Stop cancellation never commit. Keep existing error behavior: failed persist keeps modal open and meaningful error, no half-committed live state.

Tests: no-op commit, cancel after multiple changes, same preference edited via menu/in-session, unknown catalog ID preserved, duplicate Apply, mid-save failure, submission score unchanged for same profile, clothing cancel state unchanged, preset 50 mood not touched by UI.

**Gate:** All UI fronts use same data transaction API; no direct profile-write callback from row UI.
