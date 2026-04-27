---
name: create-migration
description: Scaffold a new EF Core migration for Pal.Persistence and perform a data-safety review before committing
---

The user wants to add an EF Core migration to `Pal.Persistence`.

## Steps

1. If no migration name was provided in the args, ask for one.

2. Scaffold the migration:
   ```powershell
   & "$env:USERPROFILE\.dotnet\tools\dotnet-ef.exe" migrations add <Name> `
       --project dotnet/src/Pal.Persistence `
       --startup-project dotnet/src/Pal.Api
   ```

3. Read the generated `Up()` and `Down()` in the new migration file.

4. Review for data-safety issues — flag each as PASS / WARN / FAIL:
   - **NOT NULL column without backfill**: Adding a NOT NULL column to an existing table without a default value or prior data backfill causes a migration failure on non-empty databases.
   - **Column default drop ordering**: Never drop a column default (e.g. `Guid.Empty`) in the same migration that adds a FK constraint referencing that column — drop defaults *after* rows are backfilled and the FK is verified.
   - **FK referential actions**: `Cascade` on audit event tables is wrong (audit records must outlive the entity). Use `Restrict` and require explicit cleanup. `Cascade` on data tables (uploads, jobs, etc.) is correct.
   - **Missing Down() inverse**: Every `Up()` operation must have an exact inverse in `Down()`. Missing inverses make rollbacks impossible.
   - **FK without index**: Every new FK column should have a corresponding index unless the table is tiny (<1,000 rows expected).
   - **Test fixture impact**: List any entity types touched by the migration that appear in test helper files (search for `MakeJob`, `MakeUpload`, factory helpers in `tests/`). These likely need the new field added to avoid FK or NOT NULL violations.

5. Report findings. If any FAIL items exist, ask the user to confirm before proceeding. If only WARN items, note them and continue.
