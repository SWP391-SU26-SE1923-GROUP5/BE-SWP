# AI Study Hub - API Refactoring Plan

This document outlines the architectural analysis and refactoring plan for the AI Study Hub API. The goal is to move the platform away from an entity-driven, CRUD-based architecture towards a business workflow-driven design.

## 1. Business Workflow Coverage Matrix

| Workflow | Existing APIs | Missing APIs | Refactor Priority |
| :--- | :--- | :--- | :--- |
| **1. Topic Management** | `SubjectController` (CRUD) | Specialized admin endpoints, read-only optimized endpoints for students. | Medium |
| **2. Document Upload** | `DocumentController` (CRUD) | Upload-specific endpoint with file validation, cloud storage integration, and background event triggers. | **Critical** |
| **3. AI Processing** | `DocumentChunkingController.CreateDocumentChunks` | Background task worker triggers, polling/webhook endpoints for processing status. | **Critical** |
| **4. Document Discovery** | `DocumentController.GetAll` | Dedicated discovery endpoints (Browse, Filter, Sort, Popular). | High |
| **5. Semantic Search** | `DocumentChunkingController.SearchDocumentChunks` | Dedicated `SearchController` decoupled from document chunking internals. | High |
| **6. Read Document** | None | Record view, track reading history, engagement tracking. | High |
| **7. AI Chat with Document** | `DocumentChunkingController.ChatDocument`, `ChatController` | Decoupled RAG service (currently hardcoded HTTP client in controller). | High |
| **8. Bookmark** | None | Toggle bookmark, list bookmarked documents. | Medium |
| **9. Like** | `VoteController` (CRUD) | Dedicated toggle Like/Unlike to update popularity ranking. | Medium |
| **10. Recommendation** | None | Personalized recommendation feed endpoint based on history/bookmarks. | High |
| **11. Report** | `ReportController` (CRUD) | Submit report, update report status workflow. | Low |
| **12. Moderation** | `AdminController` | Review reports, hide/restore document, moderation queue. | High |
| **13. Analytics** | `AdminController.GetDashboard` (Not Implemented) | Aggregated business stats (users, docs, AI usage). | Medium |

---

## 2. Proposed API Structure

APIs should be grouped by business capability, completely removing generic database entity endpoints.

### Documents
- `POST /api/v1/documents/upload` - Upload file and trigger background processing.
- `GET /api/v1/documents/{id}` - Get document metadata and processing status.
- `POST /api/v1/documents/{id}/read` - Record a read/view action and track engagement.

### Search
- `GET /api/v1/search` - Perform semantic search across documents.

### AI & Chat
- `GET /api/v1/ai/documents/{id}/status` - Check AI processing status (Processing, Ready, Failed).
- `POST /api/v1/ai/chat/sessions` - Start a RAG chat session with a specific document context.
- `POST /api/v1/ai/chat/sessions/{sessionId}/messages` - Send a message and get an AI response.

### Discovery & Recommendations
- `GET /api/v1/discovery/browse` - Browse documents with filters (Topic, Latest, Popular).
- `GET /api/v1/recommendations` - Get personalized document recommendations.

### Interactions (Bookmarks & Likes)
- `POST /api/v1/interactions/documents/{id}/bookmark` - Toggle bookmark status.
- `GET /api/v1/interactions/bookmarks` - View user's bookmarked documents.
- `POST /api/v1/interactions/documents/{id}/like` - Toggle like status.

### Learning History
- `GET /api/v1/history/reading` - View user's reading history.

### Moderation
- `POST /api/v1/reports` - Submit a report for a document (Student).
- `GET /api/v1/moderation/reports` - View pending reports (Moderator).
- `POST /api/v1/moderation/documents/{id}/hide` - Hide a document (Moderator).

### Analytics
- `GET /api/v1/analytics/dashboard` - Aggregated platform statistics.

### Topics
- `GET /api/v1/topics` - List available topics (Public/Student).
- `POST /api/v1/topics` - Create a new topic (Admin).
- `PUT /api/v1/topics/{id}` - Update a topic (Admin).

---

## 3. Migration Plan

For every existing endpoint:

| Controller | Action | Reasoning |
| :--- | :--- | :--- |
| `CrudControllerBase.cs` | **DELETE** | Exposes database entities directly to the client. Violates workflow-driven design and poses security/over-posting risks. |
| `DocumentController.cs` | **MODIFY** | Remove CRUD inheritance. Implement dedicated `upload` and `read` workflow endpoints. |
| `SubjectController.cs` | **MODIFY** | Rename to `TopicController` to match the ubiquitous language. Remove generic CRUD. Restrict write endpoints to Admin. |
| `DocumentChunkingController.cs` | **DELETE** | Too fat. Responsibilities must be split: search goes to `SearchController`, chat goes to `AIController`. Business logic must be moved to the Application/Business layer. |
| `ChatController.cs` | **MERGE** | Combine with the RAG logic from `DocumentChunkingController` into a unified `AIController` handling document-contextual chat. |
| `VoteController.cs` | **DELETE** | Replace with an `InteractionController` that exposes business actions (Like, Bookmark) rather than generic "Votes". |
| `ReportController.cs` | **MODIFY** | Remove CRUD. Implement a submit endpoint for students and move review/resolve endpoints to a dedicated `ModerationController`. |
| `AdminController.cs` | **MODIFY** | Expand the currently un-implemented dashboard. Add moderation and analytics workflows. |
| `UserController.cs` | **MODIFY** | Keep MediatR CQRS pattern, but replace generic `UpdateUser` with specific workflow commands (e.g., `UpdateProfile`, `ChangePassword`). |

---

## 4. Architecture Review

### Architectural Issues
*   **[Critical] Anemic Domain & CRUD-Driven Design:** The widespread use of `CrudControllerBase` forces the application into acting as a data-entry system. This bypasses rich domain behaviors and business workflows.
*   **[Critical] Fat Controllers:** `DocumentChunkingController` contains hardcoded HTTP Client calls, prompt strings, and JSON parsing logic for an external LLM. This severely violates Clean Architecture principles and Dependency Inversion.
*   **[High] Missing Background Processing Architecture:** Document processing is a heavy operation (chunking, embedding generation). Relying on synchronous API endpoints (`CreateDocumentChunks`) blocks the client and degrades UX.

### Design Flaws
*   **[High] Improper RAG Implementation:** Context building and prompt generation occur inside the API controller thread. This must be abstracted into a dedicated AI Service in the Business layer.
*   **[Medium] Inconsistent Ubiquitous Language:** The business domain calls learning categories "Topics", but the codebase uses "Subjects". This misalignment creates cognitive load for developers.

### Technical Risks
*   **[Critical] System Resilience:** The LLM integration lacks resilience policies (no Polly, no retries, no circuit breakers). If the local LLM is unresponsive, the controller fails instantly or hangs the thread pool.
*   **[High] Thread Exhaustion:** Blocking API threads while waiting for LLM completions can lead to thread pool starvation during peak usage.

### Security Risks
*   **[Medium] Over-posting Vulnerabilities:** Generic CRUD `Create` and `Update` endpoints accept DTOs that map too closely to database entities, potentially allowing malicious users to modify restricted fields (e.g., validation status, owner ID).

### Performance Bottlenecks
*   **[Medium] Synchronous Chunking:** Generating document chunks synchronously upon request will cause HTTP timeouts for larger documents. This must be offloaded to a message queue or background worker.
