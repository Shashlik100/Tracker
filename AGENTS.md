# AGENTS.md

## Project direction
This repository is a **C# WinForms desktop application** for Torah study tracking.
The direction of the project is **not** to rebuild it from scratch and **not** to convert it to a web app.
The goal is to **refactor the existing card-based product into a study-unit-based learning system**.

## Non-negotiable rules
1. Keep the existing stack: **C# + WinForms + SQLite**.
2. Reuse existing working infrastructure wherever possible:
   - library tree
   - SQLite database
   - review scheduling
   - search
   - tags
   - dashboard/statistics
   - export/backup
   - Sefaria integration
3. Do **not** keep the product centered around `Question/Answer` flashcards.
4. Refactor the product so the main unit is a **Study Unit** suitable for Torah learning.
5. Preserve full **Hebrew RTL** behavior across all screens:
   - right-aligned labels
   - RTL input and display
   - RTL layout where supported
6. Do not leave placeholder tabs or buttons in the finished result.
7. Do not use hard-coded machine-specific paths such as `C:\CodexProjects\...` for runtime logs, screenshots, verification files, or data.
8. Runtime data and logs must go to a writable application data folder.
9. Existing databases must auto-migrate safely; do not break current user data.
10. Clean the repository from generated runtime clutter:
    - screenshots
    - logs
    - database backups
    - temp artifacts
    - generated verification output
11. Strengthen `.gitignore` accordingly.
12. Prefer clear, maintainable code over clever code.
13. When changing a feature, complete the full flow end-to-end:
    - model
    - database migration
    - UI
    - save/load
    - search/filter/review if relevant
14. Remove old duplicated or obsolete code paths when they are clearly replaced.
15. The final result must build and run as a working desktop app.

## Product intent
The finished product should feel like a **Torah study system with structured learning units and scheduled review**, not like a generic flashcard app.

## Delivery expectation
When implementing, make concrete code changes, not only reports or screenshots.
Update documentation when the product behavior changes materially.
