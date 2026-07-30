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
