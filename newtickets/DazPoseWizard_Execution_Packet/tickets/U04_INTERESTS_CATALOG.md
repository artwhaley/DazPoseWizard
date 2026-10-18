# U04 — Dense scalable Interests editor

**Depends:** U03. **Goal:** Real 100+ interest choices usable without overflowing screen.

Implement search display name/ID/description, filter Enabled/Disabled/category, deterministic sort and visible count. Derive groups from `themeTags` or optional display-only category/description fields with fallback; no profile ID migration. Compact row (~52–76px at 1080p) with visible four-way NO/1x/2x/4x choice; explicit three-way sentiment Loved/Neutral/Punishment; no cycling push buttons. Preserve stored sentiment when disabled. Show text explanations in unobtrusive detail popover, selected state/focus clearly. On filter changes, draft retains values and scroll resets/preserves predictably. Use shared profile draft and prefab. No live edit to submission score or global catalog.

Tests with generated 100 and 300 entries, search nonmatches, categories, keyboard tab/arrow, toggle/no frequency, sentiment after No→1x, cancel/apply, 720p/1080p/1440p bounds and modest allocation/performance. Snapshot compare stored profiles before/after.

**Gate:** 100 entries comfortable, no clipping, all four frequency options and three sentiment options directly selectable; Settings and story prompt use one implementation.
