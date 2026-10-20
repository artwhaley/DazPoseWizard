# R01 — Large-catalog edits retain scroll, identity and focus

**Priority:** P0. **Targets:** `ProfileEditorPresenter.cs`, `RecycledListView.cs` and minimal row-view adaptations only if required.

## Defect
`ProfileEditorPresenter.Interests` and `.Equipment` rebuild the visible list on every edit. `RecycledListView.SetItems(..., resetScroll=true)` defaults to jumping to the top. This makes large catalogs nearly unusable.

## Implementation contract
1. Separate **data edit** refresh from **query/filter** refresh; when only a setting changes, do not reset scroll. Prefer an API such as `RefreshBindings()` when membership/order doesn't change; avoid resetting the entire data source unnecessarily.
2. For edits that *change* membership of the current filter (`Enabled`, `Owned`, `Reward`, etc.), anchor by stable catalog ID and preserve its top-edge position if still visible; if removed, place the nearest surviving item at a similar viewport offset, not row 0. Clamp to list bounds.
3. For changes to search text or explicit filter, reset scroll to top; the UI should show the new search results from the beginning.
4. Preserve keyboard focus/selection where possible. With virtualized/recycled rows, focus is tied to `(catalogId, controlSemantic)` rather than a pooled GameObject. Restore only to a visible, valid control after refresh, or to a predictable nearest control if filtered out.
5. Recycled row event handlers MUST be rebound to the correct item identity on scrolling; remove stale old listeners. No allocations proportional to 100–300 rows; the viewport-sized row pool remains bounded.
6. Do not alter preference enums, schema, independent equipment toggles, submission score, transaction semantics or settings vs in-session editor entry points.

## Tests
T01–T04. Make at least one test exercise item 70+ (not just the first five). Verify Apply commits and Cancel discards after scrolling. UI-dependent focus verification may remain a native gate, but managed identity/anchor policy requires automated tests.

**Done:** a value change deep in a 100-item catalog leaves the same item visible and the keyboard action predictable; search/filter changes behave intentionally.
