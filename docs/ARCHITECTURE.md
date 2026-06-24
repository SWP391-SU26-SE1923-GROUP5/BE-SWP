# Architecture Reference — AI Study Hub

## High Level Architecture

AI Study Hub uses an MVC 3-Layer architecture with strict separation between HTTP concerns, business logic, and data persistence.

```mermaid
flowchart TD
    Client[Client Applications]
    Swagger[Swagger / OpenAPI]

    subgraph Presentation["Presentation Layer - AIStudyHub.API"]
        Controllers[18 REST Controllers]
        GlobalException[GlobalExceptionMiddleware]
        FluentValidationFilter[FluentValidationFilter]
        Jwt[JWT + Google/GitHub OAuth]
        RateLimiter[Rate Limiting]
        SwaggerCfg[Swagger Configuration]
    end

    subgraph Business["Business Layer - AIStudyHub.Business"]
        Services[14+ Business Services]
        ModuleServices[ModuleServices: Document, Vote, Report, Flashcard, Quiz, Question, Answer, Submission, Notification, Payment, Subject, Tier]
        AI_Chat[AIChatService]
        AI_QuizGen[QuizAiService]
        AI_FlashGen[FlashcardAiService]
        AI_Orch[SemanticKernelOrchestrator]
        AI_KM[KernelMemoryService]
        AI_Hybrid[HybridSearchService]
        AI_Vector[QdrantVectorService]
        AI_Embed[EmbeddingService]
        AI_BM25[BM25SparseGenerator]
        AI_Rerank[RerankingService]
        AI_Faith[FaithfulnessFilter]
        AI_Grounding[GroundingVerifier]
        AI_Confidence[ConfidenceScorer]
        AI_LocalLLM[LocalAIService]
        Guardrails[Guardrails Options]
        DTOs[16 Module DTOs]
        Validators[FluentValidation Validators]
        Mappings[AutoMapper Profiles]
        MediatR[MediatR CQRS Handlers]
        Workers[3 Background Services]
        Behaviors[MediatR Pipeline Behaviors]
    end

    subgraph Data["Data Access Layer - AIStudyHub.Data"]
        UnitOfWork[Unit of Work]
        Repositories[GenericRepository + UnitOfWork]
        DbContext[ApplicationDbContext]
        Configurations[EF Core Fluent Configurations]
        Migrations[15 EF Core Migrations]
        SeedData[Admin Seed Extensions]
        Entities[18 Entity Classes]
        Enums[7 Enums]
    end

    Database[(SQL Server)]

    Client --> Controllers
    Swagger --> Controllers
    Controllers --> Jwt
    Controllers --> Services
    GlobalException --> Controllers
    Services --> AI_Orch
    Services --> AI_Chat
    Services --> AI_QuizGen
    Services --> AI_FlashGen
    AI_Orch --> AI_Hybrid
    AI_Orch --> AI_KM
    AI_Orch --> AI_LocalLLM
    AI_Orch --> AI_Faith
    AI_Orch --> AI_Grounding
    AI_Orch --> AI_Confidence
    AI_Hybrid --> AI_Vector
    AI_Hybrid --> AI_Embed
    AI_Hybrid --> AI_BM25
    AI_Hybrid --> AI_Rerank
    Services --> DTOs
    Services --> Validators
    Services --> Mappings
    Services --> MediatR
    Services --> UnitOfWork
    UnitOfWork --> Repositories
    Repositories --> DbContext
    DbContext --> Configurations
    DbContext --> SeedData
    DbContext --> Entities
    DbContext --> Enums
    DbContext --> Database
```

## Solution Structure

```text
AIStudyHub.slnx
├── AIStudyHub.API
│   ├── Controllers/              (18 REST controllers)
│   ├── DTOs/
│   ├── Extensions/               (JwtExtensions, SwaggerExtensions, RateLimitExtensions)
│   ├── Middleware/               (GlobalExceptionMiddleware, FluentValidationFilter)
│   ├── Swagger/
│   ├── Program.cs
│   └── appsettings.json
├── AIStudyHub.Business
│   ├── AI/
│   │   ├── Chat/                (AIChatService)
│   │   ├── Generators/          (QuizAiService, FlashcardAiService)
│   │   ├── Guardrails/          (FaithfulnessFilter, GroundingVerifier, ConfidenceScorer)
│   │   ├── LLM/                 (LocalAIService)
│   │   ├── Orchestration/        (SemanticKernelOrchestrator, KernelMemoryService)
│   │   ├── Search/              (HybridSearchService, RerankingService, Bm25SparseGenerator)
│   │   └── VectorStore/         (QdrantVectorService, EmbeddingService)
│   ├── Behaviors/               (MediatR pipeline behaviors)
│   ├── Configuration/           (RetrievalOptions, KernelMemoryOptions, SemanticKernelOptions, GuardrailsOptions)
│   ├── DTOs/                   (16 module DTO sets)
│   ├── Features/               (MediatR CQRS — Auth, Users)
│   ├── Interfaces/
│   │   ├── AI/                 (Chat, Generators, Guardrails, LLM, Orchestration, Search, VectorStore)
│   │   └── Services/           (All service interfaces)
│   ├── Mappings/              (AutoMapper profiles)
│   ├── Options/               (Jwt, Smtp, VnPay, Rag, ExternalAuth, Cleanup, etc.)
│   ├── Services/              (ModuleServices, AuthService, UserService, VnPayService, EmailService,
│   │                          LocalFileStorageService, DocumentProcessingService, DocumentProcessingQueue,
│   │                          BusinessServiceExtensions)
│   ├── Validators/            (16 FluentValidation module validators)
│   └── Workers/              (DocumentBackgroundProcessor, TierExpirationCleanupService,
│                             UnverifiedAccountCleanupService)
├── AIStudyHub.Data
│   ├── Configurations/        (EntityConfigurations — all 16 entity configs)
│   ├── Entities/             (18 entities + BaseEntity)
│   ├── Enums/               (7 enums)
│   ├── Extensions/           (AdminSeedExtensions, DataAccessExtensions)
│   ├── Interfaces/          (IGenericRepository, IUnitOfWork)
│   ├── Repositories/        (GenericRepository, UnitOfWork)
│   └── Migrations/          (15 EF Core migrations)
├── AIStudyHub.Tests/
├── docs/
└── README.md
```

## Layer Responsibilities

### Presentation Layer

Project: `AIStudyHub.API`

Responsibilities:
- Expose 18 REST HTTP endpoints.
- Configure Swagger/OpenAPI with JWT Bearer support and file upload support.
- Configure JWT + Google + GitHub OAuth authentication.
- Configure rate limiting (auth endpoints: 5 req/15min per IP).
- Configure global exception and validation middleware.
- Register all dependencies from Business and Data layers.
- Serve static files (uploaded documents) from `wwwroot`.
- Return HTTP responses and handle API-level concerns only.

Must not:
- Contain business rules.
- Access EF Core directly.
- Return entity classes.
- Contain SQL or repository logic.

### Business Layer

Project: `AIStudyHub.Business`

Responsibilities:
- Define DTOs and service interfaces.
- Implement all business services and workflows.
- Implement the full AI pipeline (L3-L5): embeddings, vector store, hybrid search, reranking, LLM orchestration, guardrails, quiz/flashcard generation.
- Define FluentValidation validators.
- Define AutoMapper profiles.
- Define MediatR CQRS handlers for Auth and Users.
- Implement background hosted services.
- Configure AI pipeline options.

Must not:
- Reference ASP.NET Core controller or HTTP-specific types.
- Use `DbContext` directly.

### Data Access Layer

Project: `AIStudyHub.Data`

Responsibilities:
- Define `ApplicationDbContext`.
- Define EF Core Fluent API configurations for all 18 entities.
- Implement repositories and Unit of Work.
- Register persistence dependencies.
- Manage migrations and seed data.
- Persist entities to SQL Server.

Must not:
- Contain business workflows.
- Return DTOs.
- Depend on API controllers.

## Database Design

Database engine: SQL Server.

ORM: Entity Framework Core 8 Code First.

### Entities (18)

All entities inherit from `BaseEntity` (Id: Guid, CreatedAt, UpdatedAt).

| Entity | Table | Key Fields |
|--------|-------|-----------|
| `User` | `Users` | FullName, DateOfBirth, CurrentStorageCapacity, CurrentAiTokenUsage, Status, Role, IsActive, TierId, TierExpireAt |
| `RefreshToken` | `RefreshTokens` | TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenHash |
| `OtpRecord` | `OtpRecords` | Email, OtpHash, OtpType, ExpiresAt, UsedAt, FailedAttempts, LockedUntil |
| `Subject` | `Subjects` | SubjectCode, SubjectName, Description |
| `TierMembership` | `TierMembership` | TierName, StorageLimitMb, AiTokens |
| `TierUser` | `TierUser` | UserId, TierMembershipId (join table) |
| `Document` | `Document` | UserId, SubjectId, Title, FileLink, FileName, FileExtension, FileType, FileSizeBytes, SharedUsers, ShareStatus, Status |
| `DocumentChunk` | `DocumentChunk` | DocumentId, ChunkJson, EmbeddingJson, VectorId, OrderIndex, Vector |
| `Vote` | `Votes` | UserId, DocumentId, Type (up/down) |
| `Report` | `Reports` | UserId, DocumentId, Reason |
| `Flashcard` | `Flashcard` | DocumentId, Front, Back |
| `Quiz` | `Quiz` | DocumentId, Title |
| `Question` | `Question` | QuizId, Title, Type, Position |
| `Answer` | `Answer` | QuestionId, SelectedOption, IsCorrect |
| `QuizSubmission` | `QuizSubmission` | UserId, QuizId, Answers, Score, MaxScore, TotalCorrect, GradedAt, SubmittedAt |
| `ChatSession` | `ChatSession` | UserId, DocumentId, SessionTitle |
| `ChatMessage` | `ChatMessage` | ChatSessionId, Sender, Content |
| `Notification` | `Notification` | UserId, Message, IsRead, Type |
| `Payment` | `Payment` | UserId, TierId, PaymentInfo, PaymentDate, Amount, TransactionId, Status |

### Enums (7)

`DocumentStatus` (Draft/Published/Archived/Banned/Processing/Failed), `NotificationType`, `PaymentStatus`, `QuestionType` (SingleChoice/MultipleChoice/TrueFalse), `UserRole` (Student/Educator/Admin), `ReportStatus`, `VoteType` (Upvote/Downvote).

### Entity Relationships

Note: `User` inherits `IdentityUser<Guid>` — Identity columns (NormalizedEmail, NormalizedUserName, PasswordHash, SecurityStamp, ConcurrencyStamp, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount) are inherited implicitly.

```mermaid
erDiagram
    User {
        guid Id PK
        string FullName
        string Email UK
        string NormalizedEmail
        string UserName UK
        string NormalizedUserName
        string PhoneNumber
        string PasswordHash
        datetime? LockoutEnd
        bool TwoFactorEnabled
        bool LockoutEnabled
        int AccessFailedCount
        dateonly DateOfBirth
        int CurrentStorageCapacity
        int CurrentAiTokenUsage
        string Status
        string Role
        bool IsActive
        guid TierId FK
        datetime TierExpireAt
    }

    TierMembership {
        guid Id PK
        string TierName
        int StorageLimitMb
        int AiTokens
    }

    Subject {
        guid Id PK
        string SubjectCode UK
        string SubjectName
        string Description
    }

    Document {
        guid Id PK
        guid UserId FK
        guid SubjectId FK
        string Title
        string FileLink
        string FileName
        string FileExtension
        string FileType
        bigint FileSizeBytes
        string SharedUsers
        string ShareStatus
        string Status
    }

    Payment {
        guid Id PK
        guid UserId FK
        string PaymentInfo
        datetime PaymentDate
        string Status
        guid TierId FK
        decimal Amount
        string TransactionId
    }

    RefreshToken {
        guid Id PK
        guid UserId FK
        string TokenHash
        datetime ExpiresAt
        datetime RevokedAt
        string ReplacedByTokenHash
    }

    OtpRecord {
        guid Id PK
        guid UserId FK
        string Email
        string OtpHash
        string Type
        datetime ExpiresAt
        datetime UsedAt
        int FailedAttempts
        datetime LockedUntil
    }

    Notification {
        guid Id PK
        guid UserId FK
        string Message
        bool IsRead
        string Type
    }

    Vote {
        guid Id PK
        guid UserId FK
        guid DocumentId FK
        string Type
    }

    Report {
        guid Id PK
        guid UserId FK
        guid DocumentId FK
        string Reason
    }

    Flashcard {
        guid Id PK
        guid DocumentId FK
        string Front
        string Back
    }

    Quiz {
        guid Id PK
        guid DocumentId FK
        string Title
    }

    Question {
        guid Id PK
        guid QuizId FK
        string Title
        string Type
        int Position
    }

    Answer {
        guid Id PK
        guid QuestionId FK
        string SelectedOption
        bool IsCorrect
    }

    QuizSubmission {
        guid Id PK
        guid UserId FK
        guid QuizId FK
        string Answers
        int Score
        int MaxScore
        int TotalCorrect
        datetime GradedAt
        datetime SubmittedAt
    }

    ChatSession {
        guid Id PK
        guid UserId FK
        guid DocumentId FK
        string SessionTitle
    }

    ChatMessage {
        guid Id PK
        guid ChatSessionId FK
        string Sender
        string Content
    }

    User ||--o{ Document : "owns"
    User ||--o{ Vote : "casts"
    User ||--o{ Report : "creates"
    User ||--o{ Notification : "receives"
    User ||--o{ RefreshToken : "has"
    User ||--o{ OtpRecord : "has"
    User ||--o{ Payment : "makes"
    User ||--o{ QuizSubmission : "submits"
    User ||--o{ ChatSession : "initiates"
    User }o--|| TierMembership : "subscribes_to"

    Subject ||--o{ Document : "categorizes"
    TierMembership ||--o{ Payment : "associated_with"

    Document ||--o{ Vote : "receives"
    Document ||--o{ Report : "receives"
    Document ||--o{ Flashcard : "has"
    Document ||--o{ Quiz : "has"
    Document ||--o{ ChatSession : "discusses"

    Quiz ||--o{ Question : "contains"
    Quiz ||--o{ QuizSubmission : "receives"
    Question ||--o{ Answer : "has"
    ChatSession ||--o{ ChatMessage : "contains"
```

## AI Architecture

### RAG Pipeline (L1-L5)

```mermaid
flowchart TD
    subgraph L1["L1 - Ingestion"]
        Upload[Document Upload<br/>multipart/form-data]
        Extract[Text Extraction<br/>PdfPig / OpenXML]
        Chunk[Chunking<br/>sentence-split, overlapping]
        KM[Kernel Memory Import<br/>tagged by user_id]
        Embed[Embedding via Ollama<br/>nomic-embed-text]
        Sparse[BM25 Sparse Vector]
        Qdrant[Qdrant Upsert<br/>dense + sparse]
    end

    subgraph L2["L2 - Retrieval"]
        Query[User Query]
        DenseEmb[Query Embedding]
        HybridSearch[Hybrid Search<br/>RRF fusion]
        Rerank[Reranking<br/>positional decay]
        TopK[Top-K Chunks]
    end

    subgraph L3["L3 - Generation"]
        SK[Semantic Kernel<br/>Prompt Assembly]
        LLM[Ollama LLM<br/>llama3.2:3b]
    end

    subgraph L4["L4 - Guardrails"]
        Faith[Faithfulness Filter]
        Ground[Grounding Verifier]
        Conf[Confidence Scorer]
    end

    subgraph L5["L5 - Response"]
        Resp[Response + Citations]
    end

    Upload --> Extract --> Chunk --> KM --> Embed --> Sparse --> Qdrant
    Query --> DenseEmb --> HybridSearch --> Rerank --> TopK --> SK
    SK --> LLM --> Faith --> Ground --> Conf --> Resp
```

### AI Components

| Component | Implementation | Purpose |
|-----------|---------------|---------|
| `IEmbeddingService` | `EmbeddingService` | Wraps `ILocalAIService`, generates dense embeddings |
| `IVectorStoreService` | `QdrantVectorService` | Dense/sparse upsert, ANN search, hybrid RRF search, collection mgmt |
| `ISparseVectorGenerator` | `Bm25SparseGenerator` | BM25 sparse vectors via FNV-1a hashing + sub-linear TF |
| `IHybridSearchService` | `HybridSearchService` | Orchestrates dense + sparse search with RRF |
| `IRerankingService` | `RerankingService` | Positional decay re-ranking |
| `IKernelMemoryService` | `KernelMemoryService` | Document import, search, Q&A via Kernel Memory |
| `ISemanticKernelOrchestrator` | `SemanticKernelOrchestrator` | Full L3-L5 pipeline orchestration |
| `ILocalAIService` | `LocalAIService` | Chat completion and embedding via OpenAI SDK (configured for Ollama) |
| `IFaithfulnessFilter` | `FaithfulnessFilter` | Detects evasive/uncertain answers |
| `IGroundingVerifier` | `GroundingVerifier` | Word-overlap grounding score |
| `IConfidenceScorer` | `ConfidenceScorer` | Combined confidence from grounding + faithfulness + length |
| `IQuizAiService` | `QuizAiService` | Batch-prompt quiz generation with JSON parsing + retry |
| `IFlashcardAiService` | `FlashcardAiService` | Batch-prompt flashcard generation, auto-deduplicated |

### Local AI Stack

- **Embedding Model:** `nomic-embed-text` (768 dimensions) via Ollama
- **LLM Model:** `llama3.2:3b` (configurable) via Ollama
- **Vector DB:** Qdrant at `http://localhost:6333`
- **Ollama:** Running at `http://localhost:11434`

## Request Flow

### Standard Service Flow (Simple CRUD)

```text
Client -> Controller -> Service -> Repository -> DbContext -> SQL Server
```

### CQRS Flow (Auth, Users)

```text
Client -> Controller -> MediatR -> Command/Query Handler -> Repository -> DbContext -> SQL Server
```

### AI Document Ingestion Flow

```text
Upload (multipart/form-data)
  -> DocumentUploadController
  -> LocalFileStorageService (save file)
  -> DocumentService (create DB record, status=Processing)
  -> DocumentProcessingQueue (enqueue job)
  -> DocumentBackgroundProcessor (dequeue)
    -> DocumentProcessingService (extract text)
    -> KernelMemoryService (import, tag by user_id)
    -> EmbeddingService (dense embedding)
    -> Bm25SparseGenerator (sparse vector)
    -> QdrantVectorService (upsert dense + sparse)
  -> DocumentService (update status=Published)
```

### AI Query Flow (RAG)

```text
User Question
  -> RagController / ChatController
  -> HybridSearchService (embed query + BM25 sparse)
  -> QdrantVectorService (hybrid RRF search)
  -> RerankingService (positional decay)
  -> SemanticKernelOrchestrator
    -> Assemble prompt with top-K chunks
    -> LocalAIService (LLM call)
    -> FaithfulnessFilter (validate)
    -> GroundingVerifier (validate)
    -> ConfidenceScorer (score)
  -> Response with citations
```

## Authentication Flow

```text
Client -> AuthController
  -> AuthService.RegisterAsync (create user, send OTP)
  -> VerifyRegistrationOtpAsync (validate OTP)
  -> AuthService.LoginAsync (validate credentials, issue JWT + refresh token)
  -> RefreshTokenAsync (rotate refresh token)
  -> ExternalCallback (Google/GitHub OAuth)
  -> ForgotPasswordAsync / ResetPasswordAsync (OTP flow)
  -> ChangePasswordAsync / LogoutAsync
```

JWT tokens: short-lived access tokens (60 min default) + long-lived refresh tokens (7 days), stored as SHA-256 hashes in the database.

## Payment Flow

```mermaid
sequenceDiagram
    participant Student
    participant API
    participant VNPay
    participant DB

    Student->>API: POST /api/Payment/create-checkout-url
    API->>VNPay: Build signed payment URL (HMAC-SHA512)
    VNPay-->>Student: Redirect to VNPay checkout
    Student->>VNPay: Complete payment
    VNPay->>API: GET /api/Payment/vnpay-return (server redirect)
    VNPay--)API: GET /api/Payment/vnpay-ipn (background webhook)
    API->>API: Validate VNPay signature
    API->>DB: Update Payment.Status=Completed
    API->>DB: Upgrade User.TierId + set TierExpireAt
    API-->>Student: Success page
```

## Coding Standards

- Use C# 12-compatible style where supported by .NET 8.
- Use nullable reference types.
- Prefer async APIs for I/O.
- Include `CancellationToken` in async controller, service, and repository methods.
- Use constructor injection.
- Keep controllers thin.
- Keep service methods focused on one use case.
- Return DTOs from services and controllers.
- Do not expose entities from API responses.
- Use PascalCase for public members and types.
- Use camelCase for locals and parameters.
- Use explicit access modifiers.
- Avoid static state for request-specific behavior.
- Avoid circular project references.

## Dependency Injection Strategy

DI registration locations:

- API services: `AIStudyHub.API/Program.cs`
- JWT + OAuth: `AIStudyHub.API/Extensions/JwtExtensions.cs`
- Swagger: `AIStudyHub.API/Extensions/SwaggerExtensions.cs`
- Rate limiting: `AIStudyHub.API/Extensions/RateLimitExtensions.cs`
- Business services: `AIStudyHub.Business/Services/BusinessServiceExtensions.cs`
- Data access: `AIStudyHub.Data/Extensions/DataAccessExtensions.cs`

Lifetimes:

- Controllers: framework-created.
- Services: scoped.
- Repositories: scoped.
- Unit of Work: scoped.
- DbContext: scoped.
- Validators: registered from assembly.
- AutoMapper: registered from Business assembly.
- Kernel Memory: singleton.
- Document Processing Queue: singleton.

Rules:

- Register abstractions, not only concrete classes.
- Business services should depend on interfaces.
- Data access should be hidden behind repository and Unit of Work abstractions.
- `IKernelMemory` is singleton but wraps scoped `IServiceProvider` for scoped dependencies.

## Error Handling Strategy

Global exception handling is centralized in `GlobalExceptionMiddleware`.

Rules:

- Controllers should not use broad try/catch blocks.
- Validation failures (`ValidationException`) return `400 BadRequest`.
- Authentication failures return `401 Unauthorized`.
- Authorization failures return `403 Forbidden`.
- Missing resources (`KeyNotFoundException`) return `404 NotFound`.
- Business conflicts (`InvalidOperationException`) return `409 Conflict`.
- Unexpected failures return `500 InternalServerError`.
- Production error responses must not expose stack traces.
- Error responses are consistent JSON: `{ "statusCode": ..., "message": "..." }`.
- `FluentValidationFilter` intercepts requests before action execution.

## Logging Strategy

Logging provider: Serilog.

Configuration file: `AIStudyHub.API/appsettings.json`.

Rules:

- Use structured logging with message templates.
- Use request logging middleware (`UseSerilogRequestLogging`).
- Log unhandled exceptions in global exception middleware.
- Add contextual logs around important workflows: AI generation, payment processing, document processing.
- Do not log: passwords, password hashes, JWTs, API keys, payment secrets, raw card data, private document contents.

Recommended log levels:

- `Information`: normal business events, startup configuration.
- `Warning`: suspicious or recoverable issues (e.g., document processing failure, expired tier).
- `Error`: failed operations requiring investigation.
- `Debug`: local development diagnostics only.

## Background Services

1. **DocumentBackgroundProcessor** (`BackgroundService`)
   - Reads from `IDocumentProcessingQueue` (bounded channel).
   - Processes: text extraction, Kernel Memory import, embedding, Qdrant upsert.
   - Updates document status to Published or Failed.
   - Graceful error handling per job.

2. **TierExpirationCleanupService** (`BackgroundService`)
   - Runs every `TierExpirationCheckIntervalHours` (default 24h).
   - Finds users where `TierExpireAt < UtcNow` and not on Free tier.
   - Downgrades to Free tier, clears expiration date.

3. **UnverifiedAccountCleanupService** (`BackgroundService`)
   - Runs daily at midnight UTC.
   - Finds users where `!EmailConfirmed && CreatedAt < cutoffDate` (default 7 days).
   - Cascades deletes: OtpRecords, Qdrant vectors, files, DocumentChunks, Documents, Flashcards, UserRoles, Notifications, User.

## Future Scalability

Recommended evolution paths:

- Add caching (Redis) for frequently accessed public document metadata.
- Add integration tests with a test database.
- Add unit tests for validators, business rules, and repository behavior.
- Add health checks for SQL Server, Qdrant, and Ollama.
- Add rate limiting on AI and upload endpoints.
- Add API versioning before public clients depend on the API.
- Add observability with metrics (Prometheus) and distributed tracing.
- Add object storage (Azure Blob / S3) for production file storage.
- Add audit logging for admin and payment actions.
- Add message queue (RabbitMQ / Azure Queue) for resilient background processing.
- Split AI provider implementations behind interfaces for multi-provider support.
- Keep the current 3-layer architecture unless scaling requirements justify a larger architecture.
