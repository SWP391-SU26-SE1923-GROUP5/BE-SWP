# Backend API Contract

All routes below require a bearer token. User ownership is derived from the JWT; clients cannot select another user by query string or request body.

## Quiz submissions

### Submit a quiz attempt

`POST /api/Quiz/{quizId}/submit`

The route and JWT override `QuizId` and `UserId` from the body. `DurationSeconds` is optional; when present it must be from 1 through 86,400. `Answers` is a JSON-encoded `Dictionary<string,string>` whose keys are question GUIDs and whose values are selected option text. `{}` is valid and represents an unanswered quiz.

The saved submission and all subsequent history responses use the persisted `DurationSeconds` value.

### List the current user's quiz history

- `GET /api/QuizSubmission/my?quizId={quizId}&fromDate={utc}&toDate={utc}&offset=0&limit=20`
- `GET /api/Quiz/{quizId}/history?fromDate={utc}&toDate={utc}&offset=0&limit=20`

Both routes return only submissions owned by the authenticated user. History items include quiz, document, subject code, score, persisted duration, grading time, and submission time.

### Open an owned quiz attempt

`GET /api/QuizSubmission/{submissionId}`

Returns `404 Not Found` when the submission is absent or belongs to another user. A successful response has this shape:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "quizId": "00000000-0000-0000-0000-000000000000",
  "quizTitle": "Example quiz",
  "documentId": "00000000-0000-0000-0000-000000000000",
  "documentTitle": "Example document",
  "subjectId": "00000000-0000-0000-0000-000000000000",
  "subjectCode": "SUB101",
  "subjectName": "Example subject",
  "score": 1,
  "maxScore": 2,
  "totalCorrect": 1,
  "durationSeconds": 95,
  "percentageScore": 50.0,
  "gradedAt": "2026-07-30T06:00:00Z",
  "submittedAt": "2026-07-30T06:00:00Z",
  "questions": [
    {
      "questionId": "00000000-0000-0000-0000-000000000000",
      "title": "Question text",
      "type": 0,
      "position": 1,
      "options": [
        {
          "answerId": "00000000-0000-0000-0000-000000000000",
          "text": "Option text",
          "isSelected": true,
          "isCorrect": true
        }
      ]
    }
  ]
}
```

Questions are ordered by position, and options by creation time and ID. Selection is reconstructed from persisted option text using the same ordinal, case-insensitive comparison as grading. Invalid legacy answer JSON returns HTTP `500` with stable payload:

```json
{
  "statusCode": 500,
  "message": "Stored quiz answers are invalid.",
  "error": "CorruptedQuizSubmission"
}
```

## Flashcard review history

### Review a flashcard

`POST /api/FlashcardReview/review`

`TimeSpentSeconds` is optional and, when supplied, must be from 1 through 86,400. The user may review a card only when its document is owned by them, public, or explicitly shared with them. Missing and inaccessible cards return the same not-found-style service error.

Every successful review appends one immutable attempt containing the submitted quality, optional duration, XP reconciliation value, and the complete SM-2 schedule before and after the review. `CreatedAt` is the review event time exposed as `ReviewedAt`.

### List attempts

`GET /api/FlashcardReview/history`

Optional query parameters:

- `documentId`
- `flashcardId`
- `fromDate`
- `toDate`
- `offset` (default `0`, negative values normalize to `0`)
- `limit` (default `20`, constrained to `1..100`)

Results are owner-scoped and ordered by review time descending, then attempt ID descending.

### Open an attempt

`GET /api/FlashcardReview/history/{attemptId}`

Returns the document and subject identity, card front/back, quality, duration, XP, and all before/result SM-2 snapshot values. Missing and other-user attempts both return `404 Not Found`.

### Current-user statistics

`GET /api/FlashcardReview/stats`

Statistics always use the authenticated JWT user. The former arbitrary-user route is not available.

## Delete a complete flashcard deck

`DELETE /api/Flashcard/by-document/{documentId}`

Only the document owner may delete its deck. Missing and other-user documents both return `404 Not Found`. Success returns `204 No Content`, including when the existing document already has no cards.

Deletion removes every flashcard for that document. Database cascades remove each card's current `FlashcardReview` and immutable `FlashcardReviewAttempt` rows. It does not delete or modify the document, uploaded file, vector chunks, quizzes, questions, answers, quiz submissions, study logs, subject, or other document resources.

## Page-aware document chat

`POST /api/Chat/messages` and `GET /api/Chat/sessions/{sessionId}/messages` return the same reduced `ChatMessageResponseDto` shape:

```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "chatSessionId": "00000000-0000-0000-0000-000000000000",
  "sender": "assistant",
  "content": "Grounded answer text",
  "createdAt": "2026-07-30T07:30:00Z",
  "updatedAt": null,
  "isRelevant": true
}
```

Chat responses do not include a `citations` property, source snippets, highlight flags, source arrays, or bracketed source markers. Normal answers do not automatically name a document or page.

When the user explicitly asks where content appears, the assistant may state a page only from a positive `pageNumber` attached to a supporting PDF or OCR chunk. It never derives a page from `chunkIndex`, text order, surrounding text, document length, or model knowledge. If every supporting chunk has no trustworthy page metadata—as with DOCX, TXT, or legacy vectors—the answer states that the exact page is unavailable.

Migration `20260730071539_RemoveChatCitations` drops only the obsolete chat-citation table. It does not delete or rewrite `ChatMessage`, `ChatSession`, or stored message text, so chat history content remains available after deployment.

## Hybrid document search

`POST /api/AI/rag/ask` remains a search-only endpoint. Each item in `results` contains exactly these fields:

```json
{
  "content": "matching document text",
  "score": 0.85,
  "documentId": "00000000-0000-0000-0000-000000000000",
  "fileName": "document.pdf",
  "pageNumber": 12,
  "chunkIndex": 22,
  "matchType": "semantic"
}
```

`pageNumber` and `chunkIndex` remain diagnostic retrieval metadata and may be `null`. Hybrid search no longer returns `isHighlightable`.

## Schema deployment

Migration `20260730063555_AddLearningHistoryDetails` adds nullable quiz duration storage and the flashcard review-attempt table. The application does not apply this migration as part of these API requests; the repository owner must apply it explicitly to the target database before using the new persistence paths.

Migration `20260730071539_RemoveChatCitations` removes the obsolete chat-citation table while preserving chat sessions and message text. It is also generated but not applied automatically.
