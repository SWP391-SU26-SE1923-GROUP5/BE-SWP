# Task 1 Report: Exact Upload Limit

## Changes

- Made `DocumentStorageOptions.MaxFileSizeBytes` default to 5 MiB (`5 * 1024 * 1024`).
- Removed `RagOptions.MaxFileSizeBytes`.
- Set `DocumentStorage:MaxFileSizeBytes` to `5242880`.
- Set the multipart body limit to 6 MiB to accommodate request overhead.
- Updated `DocumentController` to enforce the limit from `DocumentStorageOptions` and removed its unused `RagOptions` dependency.

## Verification

- Confirmed all runtime `MaxFileSizeBytes` references are through `DocumentStorageOptions`.
- Ran the prescribed solution build and whitespace check before committing.
