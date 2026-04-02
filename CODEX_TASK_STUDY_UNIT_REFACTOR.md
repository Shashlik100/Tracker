# CODEX TASK - STUDY UNIT REFACTOR

## Goal
Refactor the existing WinForms + SQLite Torah study tracker from a flashcard-centered app into a study-unit-centered app.
Do not rebuild from scratch. Do not convert to web. Reuse the current infrastructure.

## Keep and reuse
- library tree
- SQLite database
- review scheduling
- search
- tags
- dashboard/statistics
- export/backup
- Sefaria integration
- Hebrew RTL UI

## Core product change
The app must stop centering the product around `Topic / Question / Answer` flashcards.
The main unit should become a **Study Unit** for Torah learning.

## New content fields
Add fields such as:
- `SourceText`
- `PshatText`
- `KushyaText`
- `TerutzText`
- `ChidushText`
- `PersonalSummary`
- `ReviewNotes`

Keep existing review metadata such as:
- `Topic`
- `DueDate`
- `Level`
- `TotalReviews`
- `RepetitionCount`
- `Lapses`
- `EaseFactor`
- `IntervalDays`
- `LastRating`
- `LastReviewedAt`
- tags
- subject/library linkage

## Database migration
Do not break existing databases.
Add safe schema migration.
For legacy records, migrate by default as follows:
- `Question` -> `SourceText`
- `Answer` -> `PersonalSummary`
Other new fields may start empty.
A gradual migration is acceptable if it is safer.

## Required code areas
At minimum review and update:
- `TrackerApp/Models.cs`
- `TrackerApp/AppDatabase.cs`
- `TrackerApp/AppDatabase.Library.cs`
- `TrackerApp/StudyCardControl.cs`
- `TrackerApp/MainForm.cs`
- add/edit form(s)
- search/review/tag/export code paths as needed

## UI requirements
The UI should feel like structured Torah learning, not generic flashcards.
Replace question/answer language where practical with study-oriented language.
Preserve Hebrew RTL behavior across all screens.
Do not leave placeholder tabs or buttons in the final result.

## Add/Edit form
Convert the add/edit flow from a question/answer form into a study unit form with sections such as:
- Topic
- Source
- Pshat
- Kushya
- Terutz
- Chidush
- Personal Summary
- Review Notes
- Subject selection
- Tags

## Main unit display
Refactor `StudyCardControl` so it displays a structured study unit.
Suggested visible sections:
- library path
- topic
- next review date
- source
- pshat
- kushya
- terutz
- chidush
- personal summary
- review notes
- tags
- review metadata
Hide empty sections gracefully.

## Review screen
You may keep the internal SM-2 style engine if helpful, but the user-facing review UI should use learning-oriented labels, not flashcard language.
Map UI labels to existing enum values internally if needed.

## Search
Expand search so it covers:
- `Topic`
- `SourceText`
- `PshatText`
- `KushyaText`
- `TerutzText`
- `ChidushText`
- `PersonalSummary`
- `ReviewNotes`
- library path
- tags

## Tags
Keep tags fully working end-to-end:
- tag management
- tag assignment
- search/filter by tag
- no regression

## Library tree
Do not break the tree.
Keep lazy loading, filtering by selected node, and Sefaria integration working.

## Dashboard and export
Keep dashboard and export/backup working.
Update wording and exported fields where necessary to match the new study-unit model.

## Technical cleanup
Remove hard-coded runtime paths such as `C:\CodexProjects\...`.
Use a writable application data directory instead.
Clean the repo from runtime clutter where practical:
- screenshots
- logs
- db backups
- generated verification output
Also strengthen `.gitignore`.

## Acceptance criteria
The task is done only when all of the following are true:
- app builds successfully
- app opens successfully
- a new study unit can be created
- an existing study unit can be edited
- data is saved and loaded correctly in SQLite
- existing databases auto-migrate safely
- search works on the new fields
- tags still work
- review still works
- tree still works
- Hebrew RTL still works
- the product no longer feels like a generic question/answer flashcard app

## Recommended order
1. schema/model migration
2. insert/select/update/search/export changes
3. add/edit form refactor
4. study unit display refactor
5. main form wording and flow adjustments
6. review screen adjustments
7. path cleanup and repo cleanup
8. final build and end-to-end verification
