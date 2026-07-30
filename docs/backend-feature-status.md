# Backend Feature Status

## Learning history and deck management

| Capability | Status | Notes |
| --- | --- | --- |
| Persist quiz attempt duration | Implemented | Optional `DurationSeconds` is validated and returned from persisted quiz submissions. |
| Owned quiz history detail | Implemented | JWT-scoped detail includes document, subject, questions, options, selection state, correctness, and duration. Cross-owner access returns `404`. |
| Corrupt quiz-answer handling | Implemented | Invalid legacy JSON returns stable `CorruptedQuizSubmission` HTTP `500` without raw answers or internal exception details. |
| Append-only flashcard attempts | Implemented | Each completed review records quality, duration, XP, and before/result SM-2 snapshots. |
| Flashcard history list/detail | Implemented | JWT-scoped list supports document, card, and date filters with bounded pagination; detail is owner-filtered in the database. |
| Flashcard statistics | Implemented | `GET /api/FlashcardReview/stats` always uses the authenticated user. |
| Complete deck deletion | Implemented | Owner-only deletion removes cards and cascaded review state/history while preserving document and quiz resources. |
| Learning-history schema | Generated, pending deployment | Migration `20260730063555_AddLearningHistoryDetails` must be applied manually by the repository owner to each intended database. |

The backend build succeeds. No database migration is applied automatically by the application or by the feature implementation workflow.

## Page-aware chat and hybrid search

| Capability | Status | Notes |
| --- | --- | --- |
| Reduced chat response | Implemented | Create-message and history responses contain message identity, sender, content, timestamps, and relevance only; no `citations` property is returned. |
| Grounded normal answers | Implemented | Normal answers do not automatically name documents/pages or append source markers and source lists. |
| Explicit page questions | Implemented | A page may be stated only from positive PDF/OCR `pageNumber` metadata; DOCX, TXT, and legacy chunks without that metadata report that the exact page is unavailable. `chunkIndex` is never treated as a page. |
| Hybrid search diagnostics | Implemented | Results retain `PageNumber` and `ChunkIndex` but no longer expose `IsHighlightable`. |
| Existing chat text | Preserved | `RemoveChatCitations` drops only obsolete source-snapshot rows; stored `ChatMessage` and `ChatSession` rows survive. |
| Chat schema cleanup | Generated, pending deployment | Migration `20260730071539_RemoveChatCitations` must be applied manually by the repository owner to each intended database. |
