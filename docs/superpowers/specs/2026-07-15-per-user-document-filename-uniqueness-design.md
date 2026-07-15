# Per-user active document filename uniqueness

## Goal

Make `Document.FileName` unique among a user's active documents while preserving `Document.Title` exactly as entered. Duplicate uploads receive the smallest available numeric suffix, such as `abc.pdf`, `abc (1).pdf`, and `abc (2).pdf`.

Citation identity remains `DocumentId`. `Source` continues to be a display label derived from `FileName` and must never be used as an identifier.

## Scope

- Apply uniqueness per user, not globally.
- Compare filenames case-insensitively.
- Only documents whose `LifecycleStatus` is `Active` reserve a filename.
- Trashed documents release their filenames.
- Restoring a trashed document automatically assigns the smallest available suffix if its former filename is occupied.
- Keep `Title`, `FileLink`, physical storage naming, and Qdrant document identity unchanged.
- Avoid unrelated controller, storage, RAG, or document-domain refactoring.

## Architecture

The filename policy belongs in the existing `DocumentService`, which already owns document lifecycle rules. Add a narrowly scoped method to `IDocumentService` for obtaining an available filename. The method accepts the owner ID, requested filename, an optional document ID to exclude during restore, and a cancellation token.

The upload action calls this service method before creating its `Document`. `RestoreAsync` reuses the same logic while excluding the document being restored. Filename parsing and suffix selection remain private implementation details of `DocumentService`; no new service, project, or dependency-injection registration is introduced.

Add a filtered SQL Server unique index on `(UserId, FileName)` for rows where `LifecycleStatus` is `Active` and `FileName` is not null. This index is the final concurrency safeguard. Filename comparison must follow a case-insensitive SQL Server collation so application lookup and database enforcement agree.

## Filename allocation

1. Reduce the supplied value to a safe display filename with `Path.GetFileName`.
2. Preserve the extension and treat the remainder as the stem.
3. Query active filenames owned by the user, excluding `excludeDocumentId` when supplied.
4. Compare names case-insensitively.
5. Return the requested filename when available.
6. Otherwise, test `stem (1).ext`, `stem (2).ext`, and so on, returning the first available name.

An explicitly supplied suffixed name is treated as its own requested base. For example, if `abc (1).pdf` already exists, another upload named `abc (1).pdf` becomes `abc (1) (1).pdf`. The allocator does not reinterpret user-supplied suffixes, which avoids surprising renumbering of intentionally named files.

Names without extensions and names containing multiple dots follow normal `Path.GetFileNameWithoutExtension` and `Path.GetExtension` behavior. The final filename must respect the existing 255-character database limit; when a suffix is needed, truncate only the stem enough to fit the suffix and extension.

## Data flows

### Upload

The controller validates the upload as it does today, requests an available filename from `IDocumentService`, then stores that value in `Document.FileName`. The same allocated filename is included in `DocumentProcessRequest`, so newly indexed Qdrant chunks expose the normalized display name as `Source`.

If a concurrent request claims the same name after allocation, the unique index rejects the write. The operation recalculates and retries at most three times. If all attempts collide, return a conflict-style business error rather than persisting duplicate active names.

### Trash and restore

Changing `LifecycleStatus` from `Active` to `Trashed` releases the filtered unique-index entry. Uploading another document may then reuse the old filename.

Before changing a trashed document back to `Active`, `RestoreAsync` allocates a filename while excluding that document ID. It updates `FileName` when necessary, then restores the lifecycle fields. A concurrent collision uses the same bounded retry policy.

Restoration does not change `Title`, `FileLink`, or the physical file. Existing restore/reindex behavior is otherwise unchanged.

### Citations

Citation DTOs use `DocumentId` as the stable identity. `Source` is display-only. Active filenames being unique per user improves readability but is not an identity guarantee across users, deleted history, or time.

## Error handling and compatibility

- Reject or safely fall back for an empty filename according to the existing upload validation; do not introduce a new public naming endpoint.
- Convert exhausted uniqueness retries into a clear conflict response without exposing database exception details.
- Preserve existing documents and migration data. Before creating the unique index, the migration must resolve any pre-existing active duplicates deterministically by creation order: the earliest document keeps its name, and later documents receive the smallest available suffix.
- Trashed duplicates require no migration rename because the filtered index excludes them.

## Tests

Cover the filename allocator and affected flows:

- Repeated uploads by one user produce `abc.pdf`, `abc (1).pdf`, and `abc (2).pdf`.
- Different users may each own active `abc.pdf`.
- Case variants collide.
- A user-supplied `abc (1).pdf` is treated as a literal base name.
- Multiple-dot and extensionless filenames are handled correctly.
- Generated suffixes stay within 255 characters.
- Trashed documents do not reserve filenames.
- Restore keeps the former name when free and renames when occupied.
- Database uniqueness prevents duplicate active `(UserId, FileName)` values.
- Citation responses retain the correct `DocumentId` and display filename.

## Non-goals

- Making `Title` unique.
- Global filename uniqueness across users.
- Renaming physical files or changing storage URLs.
- Using filenames as citation or document identifiers.
- Refactoring the full upload workflow or changing Qdrant schema beyond using the allocated filename for new/reprocessed chunks.
