# Architecture Reference — AI Study Hub

> **Cập nhật**: 2026-06-28
> **Base URL (dev - HTTP)**: `http://localhost:5171` · **HTTPS**: `https://localhost:7265` · **SignalR**: `/hubs/notifications`
> **AI provider**: OpenAI SDK (`text-embedding-3-small` + `gpt-4o-mini`) + Qdrant
> **Layer count**: 3 (API / Business / Data) — strictly enforced

## High Level Architecture

AI Study Hub uses an MVC 3-Layer architecture with strict separation between HTTP concerns, business logic, and data persistence.

```mermaid
flowchart TD
    Client[Client Applications]
    Swagger[Swagger / OpenAPI]

    subgraph Presentation["Presentation Layer - AIStudyHub.API"]
        Controllers[19 REST Controllers]
        Hubs[SignalR Hubs - NotificationsHub]
        GlobalException[GlobalExceptionMiddleware]
        FluentValidationFilter[FluentValidationFilter]
        Jwt[JWT + Google/GitHub OAuth]
        RateLimiter[Rate Limiting]
        SwaggerCfg[Swagger Configuration]
    end

    subgraph Business["Business Layer - AIStudyHub.Business"]
        Services[16+ Business Services]
        ModuleServices["ModuleServices: Document, Vote, Report, Flashcard, Quiz, Question, Answer, Submission, Notification, Payment, Subject, Tier, Gamification, FlashcardReview, Recommendation"]
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
        DTOs["21 Module DTOs (+Gamification, FlashcardReview, Recommendations, UserStats)"]
        Validators[FluentValidation Validators]
        Mappings[AutoMapper Profiles]
        MediatR[MediatR CQRS Handlers]
        Workers[4 Background Services]
        Behaviors[MediatR Pipeline Behaviors]
        RT[IRealTimeNotificationService - SignalR push]
    end

    subgraph Data["Data Access Layer - AIStudyHub.Data"]
        UnitOfWork[Unit of Work]
        Repositories[GenericRepository + UnitOfWork]
        DbContext[ApplicationDbContext]
        Configurations[EF Core Fluent Configurations]
        Migrations["21 EF Core Migrations"]
        SeedData[Admin Seed Extensions]
        Entities["21 Entity Classes (incl. UserStats, FlashcardReview, StudyLog)"]
        Enums["8 Enums (incl. ActivityType, ShareStatus)"]
    end

    Database[(SQL Server)]

    Client --> Controllers
    Client -. WebSocket .-> Hubs
    Swagger --> Controllers
    Controllers --> Jwt
    Controllers --> Services
    Services --> RT
    RT --> Hubs
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
│   ├── Controllers/              (19 REST controllers)
│   ├── DTOs/
│   ├── Extensions/               (JwtExtensions, SwaggerExtensions, RateLimitExtensions)
│   ├── Hubs/                     (NotificationsHub - SignalR implementation)
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
│   ├── DTOs/                   (21 module DTO sets - incl. Gamification, FlashcardReviews, Recommendations, UserStats)
│   ├── Features/               (MediatR CQRS — Auth, Users)
│   ├── Hubs/                   (INotificationsHub abstraction - signal target)
│   ├── Interfaces/
│   │   ├── AI/                 (Chat, Generators, Guardrails, LLM, Orchestration, Search, VectorStore)
│   │   └── Services/           (All service interfaces - incl. IGamificationService, IFlashcardReviewService, IRecommendationService)
│   ├── Mappings/              (AutoMapper profiles)
│   ├── Options/               (Jwt, Smtp, VnPay, Rag, ExternalAuth, Cleanup, etc.)
│   ├── Services/              (ModuleServices, AuthService, UserService, VnPayService, EmailService,
│   │                          LocalFileStorageService, DocumentProcessingService, DocumentProcessingQueue,
│   │                          GamificationService, FlashcardReviewService, RecommendationService,
│   │                          RealTimeNotificationService, BusinessServiceExtensions)
│   ├── Validators/            (FluentValidation module validators)
│   └── Workers/              (DocumentBackgroundProcessor, TierExpirationCleanupService,
│                             UnverifiedAccountCleanupService, DailyStreakResetWorker)
├── AIStudyHub.Data
│   ├── Configurations/        (EntityConfigurations)
│   ├── Entities/             (21 entities + BaseEntity - incl. UserStats, FlashcardReview, StudyLog)
│   ├── Enums/               (8 enums - incl. ActivityType, ShareStatus)
│   ├── Extensions/           (AdminSeedExtensions, DataAccessExtensions)
│   ├── Interfaces/          (IGenericRepository, IUnitOfWork)
│   ├── Repositories/        (GenericRepository, UnitOfWork)
│   └── Migrations/          (21 EF Core migrations)
├── docs/                     (FRONTEND_GUIDE.md, EF_MIGRATION_COMMANDS.md, ...)
├── AGENT.md                  (coding conventions & rules)
├── ARCHITECTURE.md           (this file)
└── README.md
```

## Layer Responsibilities

### Presentation Layer

Project: `AIStudyHub.API`

Responsibilities:
- Expose 19 REST HTTP endpoints.
- Configure Swagger/OpenAPI with JWT Bearer support and file upload support.
- Configure JWT + Google + GitHub OAuth authentication.
- Configure rate limiting (auth endpoints: 5 req/15min per IP).
- Configure global exception and validation middleware.
- Register all dependencies from Business and Data layers.
- Serve static files (uploaded documents) from `wwwroot` (request path `/uploads`).
- Host SignalR hubs (`Hubs/NotificationsHub`) and map them at top-level routes (e.g. `/hubs/notifications`).
- Return HTTP responses and handle API-level concerns only.

Must not:
- Contain business rules.
- Access EF Core directly.
- Return entity classes.
- Contain SQL or repository logic.
- Inject SignalR `Hub` types into business services (use `IRealTimeNotificationService` instead).

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

### Entities (21)

All entities inherit from `BaseEntity` (Id: Guid, CreatedAt, UpdatedAt), **except** `User` which inherits `IdentityUser<Guid>` and carries the ASP.NET Identity columns implicitly.

| Entity | Table | Key Fields |
|--------|-------|-----------|
| `User` | `Users` | FullName, DateOfBirth, CurrentStorageCapacity, CurrentAiTokenUsage, Status, Role, IsActive, TierId, TierExpireAt |
| `RefreshToken` | `RefreshTokens` | TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenHash |
| `OtpRecord` | `OtpRecords` | Email, OtpHash, OtpType, ExpiresAt, UsedAt, FailedAttempts, LockedUntil |
| `Subject` | `Subjects` | OwnerUserId (required), SubjectCode (unique per owner), SubjectName, Description |
| `TierMembership` | `TierMembership` | TierName, StorageLimitMb, AiTokens |
| `TierUser` | `TierUser` | UserId, TierMembershipId (join table) |
| `Document` | `Document` | UserId, SubjectId, Title, FileLink, FileName, FileExtension, FileType, FileSizeBytes, SharedUsers, ShareStatus, Status |
| `DocumentChunk` | `DocumentChunk` | DocumentId, ChunkJson, EmbeddingJson, VectorId, OrderIndex, Vector |
| `Vote` | `Votes` | UserId, DocumentId, Type (up/down) |
| `Report` | `Reports` | UserId, DocumentId, Reason, Status (workflow), Category |
| `Flashcard` | `Flashcard` | DocumentId, Front, Back |
| `FlashcardReview` | `FlashcardReview` | UserId, FlashcardId, EaseFactor, Interval, Repetitions, NextReviewDate (SM-2 state) |
| `Quiz` | `Quiz` | DocumentId, Title |
| `Question` | `Question` | QuizId, Title, Type, Position |
| `Answer` | `Answer` | QuestionId, SelectedOption, IsCorrect |
| `QuizSubmission` | `QuizSubmission` | UserId, QuizId, Answers, Score, MaxScore, TotalCorrect, GradedAt, SubmittedAt |
| `ChatSession` | `ChatSession` | UserId, DocumentId, SessionTitle |
| `ChatMessage` | `ChatMessage` | ChatSessionId, Sender, Content |
| `Notification` | `Notification` | UserId, Message, IsRead, Type |
| `Payment` | `Payment` | UserId, TierId, PaymentInfo, PaymentDate, Amount, TransactionId, Status |
| `UserStats` | `UserStats` | UserId (unique), TotalXp, CurrentLevel, CurrentStreak, BestStreak, LastActivityDate |
| `StudyLog` | `StudyLog` | UserId, ActivityType, DocumentId?, SubjectCode?, IsCorrect, TimeSpentSeconds?, XpEarned |

### Enums (8)

`DocumentStatus` (Draft/Published/Archived/Banned/Processing/Failed), `NotificationType` (incl. Document/Quiz/FlashcardsReady/StreakAtRisk/TierUpgraded/Payment variants), `PaymentStatus`, `QuestionType` (SingleChoice/MultipleChoice/TrueFalse), `UserRole` (Student/Admin), `ReportStatus`, `VoteType` (Upvote/Downvote), **`ActivityType`** (FlashcardReview / QuizSubmit / DocumentUpload / ChatMessage), **`ShareStatus`** (Private / Public).

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
        datetime LockoutEnd
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
        guid OwnerUserId FK
        string SubjectCode "unique per owner"
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

    UserStats {
        guid Id PK
        guid UserId FK,UK
        int TotalXp
        int CurrentLevel
        int CurrentStreak
        int BestStreak
        datetime LastActivityDate
    }

    FlashcardReview {
        guid Id PK
        guid UserId FK
        guid FlashcardId FK
        float EaseFactor
        int Interval
        int Repetitions
        datetime NextReviewDate
    }

    StudyLog {
        guid Id PK
        guid UserId FK
        string ActivityType
        guid DocumentId FK
        string SubjectCode
        bool IsCorrect
        int TimeSpentSeconds
        int XpEarned
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
    User ||--|| UserStats : "has"
    User ||--o{ FlashcardReview : "tracks"
    User ||--o{ StudyLog : "logs"
    User }o--|| TierMembership : "subscribes_to"
    User ||--o{ Subject : "owns"

    Subject ||--o{ Document : "categorizes"
    TierMembership ||--o{ Payment : "associated_with"

    Document ||--o{ Vote : "receives"
    Document ||--o{ Report : "receives"
    Document ||--o{ Flashcard : "has"
    Document ||--o{ Quiz : "has"
    Document ||--o{ ChatSession : "discusses"
    Document ||--o{ StudyLog : "referenced_by"

    Flashcard ||--o{ FlashcardReview : "scheduled_for"
    Quiz ||--o{ Question : "contains"
    Quiz ||--o{ QuizSubmission : "receives"
    Question ||--o{ Answer : "has"
    ChatSession ||--o{ ChatMessage : "contains"
```

## AI Architecture

### RAG & AI Generation Flows

#### L1 — Document Ingestion Pipeline

```mermaid
sequenceDiagram
    participant Client
    participant Controller as DocumentController
    participant Storage as LocalFileStorageService
    participant DocSvc as DocumentService
    participant Queue as DocumentProcessingQueue
    participant Processor as DocumentBackgroundProcessor
    participant KM as KernelMemoryService
    participant EmbedSvc as EmbeddingService
    participant SparseGen as Bm25SparseGenerator
    participant Qdrant as QdrantVectorService
    participant DB as DbContext

    Client->>Controller: POST /api/Document/upload/file (multipart/form-data)
    Controller->>Storage: SaveFileAsync(file)
    Storage-->>Controller: filePath

    Controller->>DocSvc: CreateAsync(dto)
    DocSvc->>DB: Insert Document (Status=Processing)
    DocSvc-->>Controller: documentId

    Controller->>Queue: EnqueueAsync(request)
    Note over Controller,DB: File and Processing document are durable before acceptance
    Controller-->>Client: 202 Accepted (documentId)

    par Async Background Processing
        Processor->>Processor: DequeueAsync() — blocking loop

        Processor->>KM: ImportDocumentAsync(filePath, docId, userId, fileName)
        Note over KM: PdfPig / OpenXML<br/>Extracts raw text
        Note over KM: Splits into chunks<br/>(sentence-split, overlapping)
        Note over KM: Tags: user_id, file_name<br/>Stores in configured backend

        Processor->>KM: SearchAsync("", userId, limit=1000)
        Note over KM: Returns Citation[] with<br/>DocumentId + Partitions[]

        loop For each Citation (one per source file)
            loop For each Partition (one per chunk)
                Processor->>EmbedSvc: GenerateEmbeddingAsync(chunkText)
                Note over EmbedSvc: Calls LocalAIService<br/>OpenAI embeddings API
                Note over EmbedSvc: Returns float[] dense vector

                Processor->>SparseGen: GenerateSparseVector(chunkText)
                Note over SparseGen: FNV-1a word hashing<br/>Sub-linear TF scoring

                Processor->>Qdrant: EnsureCollectionExistsAsync()
                Note over Qdrant: Creates collection if missing<br/>Registers sparse-text named vector

                Processor->>Qdrant: UpsertVectorAsync(id, dense, sparse, metadata)
                Note over Qdrant: Stores: dense "" vector +<br/>sparse "sparse-text" named vector<br/>Payload: documentId, userId, text,<br/>fileName, chunkIndex
            end
        end

        Processor->>DB: Update Document (Status=Published)
    end
```

#### L2-L5 — Legacy RAG Query Flow (Chat with Document)

> Current API contract: `POST /api/AI/rag/ask` is search-only and returns reranked chunks. Chat answer generation uses `POST /api/Chat/messages`. The sequence below documents the orchestration used by the chat flow, not the search-only endpoint.

```mermaid
sequenceDiagram
    participant Client
    participant Controller as ChatController
    participant Orch as SemanticKernelOrchestrator
    participant Hybrid as HybridSearchService
    participant EmbedSvc as EmbeddingService
    participant SparseGen as Bm25SparseGenerator
    participant Qdrant as QdrantVectorService
    participant Rerank as RerankingService
    participant LLM as LocalAIService
    participant Faith as FaithfulnessFilter
    participant Ground as GroundingVerifier
    participant Score as ConfidenceScorer

    Client->>Controller: POST /api/Chat/messages { sessionId, message }
    Controller->>Orch: AskWithTrackingAsync(userId, documentIds, question, history)

    rect rgb(235, 245, 255)
        Note over Orch,Qdrant: L2 — Retrieval (Hybrid Search + Reranking)

        Orch->>Hybrid: SearchAsync(question, userId, topK=10)

        par Hybrid Search
            Hybrid->>EmbedSvc: GenerateEmbeddingAsync(question)
            Note over EmbedSvc: OpenAI embedding API<br/>Returns float[] dense vector

            Hybrid->>SparseGen: GenerateSparseVector(question)
            Note over SparseGen: BM25 sparse: FNV-1a hashes<br/>+ sub-linear TF values
        end

        Hybrid->>Qdrant: HybridSearchAsync(dense, sparse, topK=20, filter)
        Note over Qdrant: Sends to Qdrant REST API:<br/>prefetch[0]: dense query<br/>prefetch[1]: sparse query<br/>query.fusion = "rrf"<br/>Returns fused + scored results

        Hybrid-->>Orch: List<SearchResult> (20 items)

        Orch->>Rerank: RerankAsync(question, results, topK=5)
        Note over Rerank: Sorts by score descending<br/>Applies positional decay:<br/>Score × (1.0 - index × 0.1)
        Rerank-->>Orch: List<SearchResult> (5 items, adjusted scores)

        alt No relevant results
            Orch-->>Controller: RagResponse("no info found", confidence=0)
        end
    end

    rect rgb(240, 255, 240)
        Note over Orch,LLM: L3 — Generation (LLM Prompt Assembly)

        Orch->>Orch: Build context string<br/>"--- Source: fileName ---\nchunkText"
        Orch->>Orch: Build system prompt:<br/>- Answer ONLY from SOURCES<br/>- Guide on AIStudyHub features<br/>- Never reveal backend details<br/>- Vietnamese by default
        Orch->>Orch: Build user prompt:<br/>"SOURCES: [chunks]\nQUESTION: [question]"

        Orch->>LLM: SendMessageAsync(systemPrompt + userPrompt)
        Note over LLM: OpenAI Chat Completions API<br/>Sends full prompt to configured model
        LLM-->>Orch: answer (string)
    end

    rect rgb(255, 245, 230)
        Note over Orch,Score: L4 — Guardrails (Faithfulness + Grounding + Confidence)

        Orch->>Faith: ValidateAsync(answer, sourceContents)
        Note over Faith: Checks for evasive phrases<br/>("cannot find", "I don't know")<br/>Returns: bool isFaithful

        Orch->>Ground: VerifyAsync(answer, searchResults)
        Note over Ground: Word-overlap scoring:<br/>Counts answer words found in source<br/>Coverage = grounded / total<br/>Returns: GroundingResult

        Orch->>Score: Score(answer, groundingResult, isFaithful)
        Note over Score: Base = grounding.Score<br/>× 0.5 if not faithful<br/>× 0.8 if answer < 50 chars<br/>+ 0.1 if above threshold<br/>Clamp(0, 1)
        Score-->>Orch: confidence (double)
    end

    rect rgb(250, 240, 255)
        Note over Orch,Client: L5 — Response

        Orch->>Orch: Build CitationInfo[]<br/>from searchResults
        Orch-->>Controller: RagResponse(answer, citations, confidence)
        Controller-->>Client: 200 OK
    end
```

#### L6 — Flashcard Generation Flow

```mermaid
sequenceDiagram
    participant Client
    participant Controller as FlashcardController
    participant FlashSvc as FlashcardAiService
    participant KM as KernelMemoryService
    participant LLM as LocalAIService
    participant DB as DbContext

    Client->>Controller: POST /api/AI/flashcards/generate?docId={guid} { numberOfFlashcards }
    Controller->>FlashSvc: GenerateFlashcardsAsync(docId, request, userId)

    FlashSvc->>FlashSvc: Require owner, Done status, and nonempty processed context
    Note over Controller,FlashSvc: numberOfFlashcards is required integer 1..20

    FlashSvc->>KM: SearchAsync("", filter=documentId, limit=1000)
    Note over KM: Retrieves all chunks for document
    KM-->>FlashSvc: MemoryAnswer.Results[]

    FlashSvc->>FlashSvc: BuildContext(citations)
    Note over FlashSvc: Concatenates all partition.Text<br/>Limited to 30,000 chars

    loop Batch generation (batchSize=5, maxAttempts=80)
        FlashSvc->>LLM: SendMessageAsync(batchPrompt, temp=0.2)
        Note over LLM: System: Extract N facts → JSON flashcards<br/>"front": question ending with ?<br/>"back": short factual answer

        LLM-->>FlashSvc: aiText (raw JSON string)

        FlashSvc->>FlashSvc: ParseFlashcardArray(aiText)
        Note over FlashSvc: 3-stage parser:<br/>1. Extract balanced [...] JSON array<br/>2. JsonDocument.Parse<br/>3. Streaming fallback for malform

        FlashSvc->>FlashSvc: Normalize & dedupe<br/>Enforce: front=question, back=answer<br/>Reject placeholders
    end

    Note over FlashSvc,DB: Require exactly requested count; otherwise 422 and no rows
    FlashSvc->>DB: Insert exactly requested Flashcard entities
    FlashSvc-->>Controller: FlashcardResponseDto[]
    Controller-->>Client: 200 OK
```

#### L6 — Quiz Generation Flow

```mermaid
sequenceDiagram
    participant Client
    participant Controller as QuizController
    participant QuizSvc as QuizAiService
    participant Qdrant as QdrantVectorService
    participant LLM as LocalAIService
    participant DB as DbContext

    Client->>Controller: POST /api/AI/quizzes/generate?docId={guid} { numberOfQuestions }
    Controller->>QuizSvc: GenerateAndPersistQuizAsync(docId, request, userId)

    QuizSvc->>QuizSvc: Require owner, Done status, and nonempty processed context
    Note over Controller,QuizSvc: numberOfQuestions is required integer 1..20

    QuizSvc->>Qdrant: GetPayloadsByDocumentIdAsync(documentId)
    Note over Qdrant: REST scroll API<br/>Returns all payload dicts for doc
    Qdrant-->>QuizSvc: List<Dictionary<string,string>>

    QuizSvc->>QuizSvc: Sort by chunkIndex<br/>Fix Mojibake (UTF-8→Latin-1→UTF-8)
    Note over QuizSvc: Mojibake pattern detection:<br/>"Ã", "Ä", "áº" sequences

    QuizSvc->>QuizSvc: Concatenate chunks as context

    loop Batch generation (batchSize=3, maxAttempts=60)
        QuizSvc->>LLM: SendMessageAsync(batchPrompt, temp=0.2)
        Note over LLM: System: Read TEXT → JSON quiz<br/>Exactly N questions<br/>Each: questionTitle + 4 answers<br/>Exactly 1 isCorrect=true

        LLM-->>QuizSvc: aiText (raw JSON string)

        QuizSvc->>QuizSvc: ParseQuizPayload(aiText)
        Note over QuizSvc: 3-stage parser:<br/>1. Extract balanced {...} object<br/>2. JsonDocument.Parse<br/>3. Streaming per question fallback

        QuizSvc->>QuizSvc: Normalize & dedupe<br/>Reject placeholders, enforce 1 correct
    end

    QuizSvc->>DB: Insert Quiz → Questions → Answers
    Note over QuizSvc,DB: Require exactly requested count; otherwise 422 and no rows
    QuizSvc-->>Controller: QuizResponseDto
    Controller-->>Client: 200 OK
```

### AI Components

| Component | Implementation | Purpose |
|-----------|---------------|---------|
| `IOpenAIService` | `OpenAIService` | Chat completion + embeddings via OpenAI SDK (`ChatClient`, `EmbeddingClient`) |
| `IEmbeddingService` | `EmbeddingService` | Wraps `IOpenAIService.CreateEmbeddingsFromTexts` for dense vector generation |
| `IVectorStoreService` | `QdrantVectorService` | Dense/sparse upsert, ANN search, hybrid RRF search via REST API, collection management |
| `ISparseVectorGenerator` | `Bm25SparseGenerator` | BM25 sparse vectors via FNV-1a 32-bit word hashing + sub-linear TF-IDF scoring |
| `IHybridSearchService` | `HybridSearchService` | Orchestrates dense + sparse search with prefetch RRF fusion in Qdrant |
| `IRerankingService` | `RerankingService` | Positional decay re-ranking: `Score × (1.0 - index × 0.1)` |
| `IKernelMemoryService` | `KernelMemoryService` | Document import (chunking + tagging), search, Q&A via `Microsoft.KernelMemory` |
| `ISemanticKernelOrchestrator` | `SemanticKernelOrchestrator` | Full L2–L5 RAG pipeline orchestration (search → rerank → LLM → guardrails → response) |
| `IFaithfulnessFilter` | `FaithfulnessFilter` | Detects evasive answers ("cannot find", "I don't know") despite available context |
| `IGroundingVerifier` | `GroundingVerifier` | Word-overlap grounding score (source words vs answer words coverage) |
| `IConfidenceScorer` | `ConfidenceScorer` | Combined confidence: grounding × faithfulness × length × threshold bonus, clamped [0,1] |
| `IQuizAiService` | `QuizAiService` | Batch-prompt quiz generation (3 questions/batch, 3 duplicate-then-abort policy, 3-stage JSON parser) |
| `IFlashcardAiService` | `FlashcardAiService` | Batch-prompt flashcard generation (5 cards/batch, Kernel Memory context, deduplication) |
| `IDocumentProcessingService` | `DocumentProcessingService` | Text extraction from PDF (PdfPig), DOCX (OpenXML), TXT/MD; sentence-split chunking with overlap |
| `IDocumentProcessingQueue` | `DocumentProcessingQueue` | Bounded in-process dispatch queue; durable `Processing` document records are recovered and re-enqueued at startup |
| `DocumentBackgroundProcessor` | `DocumentBackgroundProcessor` | `BackgroundService` — resumes durable active `Processing` documents, dequeues jobs, calls KernelMemory import + generates dense/sparse vectors → Qdrant, then sets `Done` or `Failed` |

### AI / LLM Configuration

Configuration file: `AIStudyHub.API/appsettings.json` → `RagOptions`.

| Setting | Default | Description |
|---------|---------|-------------|
| `OpenAIApiKey` | *(required)* | API key for OpenAI-compatible endpoint |
| `OpenAIChatModel` | `gpt-4o-mini` | Chat completion model (supports o1, gpt-5 families with special temperature handling) |
| `OpenAIEmbeddingModel` | `text-embedding-3-small` | Embedding model via OpenAI SDK |

| `VectorDbUrl` | `http://localhost:6333` | Qdrant REST URL |
| `VectorDbCollectionName` | `ai-study-hub` | Qdrant collection name |
| `VectorDbVectorSize` | `1536` | Dense vector dimension (matches `text-embedding-3-small` output) |

**Vector DB:** Qdrant at `http://localhost:6333` with hybrid (dense + sparse named vector) collection support.

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

See **L1 — Document Ingestion Pipeline** sequence diagram above.

### AI Query Flow (RAG)

See **L2-L5 — RAG Query Flow (Chat with Document)** sequence diagram above.

### Spaced Repetition Flow (SM-2)

```mermaid
sequenceDiagram
    participant Client
    participant Ctrl as FlashcardReviewController
    participant Svc as FlashcardReviewService
    participant UoW as IUnitOfWork
    participant DB as DbContext

    Client->>Ctrl: GET /api/FlashcardReview/due?limit=20
    Ctrl->>Svc: GetDueAsync(userId, limit)
    Svc->>DB: FlashcardReview where NextReviewDate <= UtcNow
    DB-->>Svc: due cards
    Svc-->>Ctrl: DueFlashcardDto[]
    Ctrl-->>Client: 200 OK

    loop Per card
        Client->>Ctrl: POST /api/FlashcardReview/review { flashcardId, quality }
        Ctrl->>Svc: ProcessReviewAsync(userId, flashcardId, quality)
        Note over Svc: SM-2 math:<br/>EF' = max(1.3, EF + (0.1 - (5-q)*(0.08 + (5-q)*0.02)))<br/>interval & repetitions updated<br/>NextReviewDate = UtcNow + interval days
        Svc->>DB: Upsert FlashcardReview (same user, same flashcard)
        Svc->>UoW: SaveChanges
        Svc-->>Ctrl: FlashcardReviewResponseDto (new EF, interval, nextReviewDate)
        Ctrl-->>Client: 200 OK
    end
```

### Gamification Flow (XP / Level / Streak)

```mermaid
sequenceDiagram
    participant Activity as Quiz/Flashcard/Document Service
    participant GamSvc as GamificationService
    participant UoW as IUnitOfWork
    participant DB as DbContext
    participant RT as RealTimeNotificationService
    participant Hub as NotificationsHub (SignalR)

    Activity->>GamSvc: AwardXpAsync({ userId, activityType, xpDelta, ... })
    GamSvc->>DB: SELECT UserStats FOR UPDATE
    Note over GamSvc: totalXp += xpDelta<br/>level = 1 + floor(totalXp / 100)<br/>if today != LastActivityDate:<br/>  if (today - LastActivityDate) == 1 day:<br/>    currentStreak++<br/>  else:<br/>    currentStreak = 1<br/>bestStreak = max(bestStreak, currentStreak)<br/>LastActivityDate = today
    GamSvc->>DB: INSERT StudyLog (append-only)
    GamSvc->>UoW: SaveChanges
    alt level increased
        GamSvc->>RT: PushToUserAsync(userId, { type=TierUpgraded, payload={ newLevel, totalXp } })
        RT->>Hub: Clients.Group(userId).SendAsync("ReceiveNotification", payload)
    end
    GamSvc-->>Activity: XpAwardResult { totalXp, currentLevel, leveledUp }
```

### Real-time Push Flow (SignalR)

```text
Client connects:  ws(s)://localhost:5171/hubs/notifications?access_token=<JWT>
  -> Server upgrades + auth validates token
Client -> Server: invoke("JoinGroup", userId)        // userId is a STRING
Server: store connection in group(userId)

Server -> Client: ReceiveNotification(RealTimeNotification {
  userId, title, body, type, timestamp, payload
})
```

Logout: client calls `invoke("LeaveGroup", userId)` before disposing the connection.

The `IRealTimeNotificationService.PushToUserAsync` abstraction in `AIStudyHub.Business` wraps the `INotificationsHub` SignalR context so business services can push events without referencing `HubContext` or `Microsoft.AspNetCore.SignalR` directly.

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

## Subject Ownership Contract

`GET`, `POST`, `PUT`, and `DELETE /api/Subject` are authenticated student operations. The current JWT user owns the results and all writes. Subject operations have no Admin override.

Missing or foreign IDs return `404`. A Subject referenced by a Document cannot be deleted and returns `409`. Document creation accepts only a Subject owned by the requesting student.

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

## Repository Change Policy

These rules are architectural constraints and apply to every feature, bug fix, and refactor.

### Migration History

- A new EF Core migration may be created when an approved schema change requires one.
- Every migration that already exists in the repository is immutable.
- Never edit, rename, move, regenerate, squash, or delete an existing migration `.cs` file, designer file, migration name, timestamp, ordering, or historical model operation.
- Never run `dotnet ef migrations remove` against a committed migration.
- `ApplicationDbContextModelSnapshot.cs` may change only as the generated result of adding a new migration. Never edit it manually to rewrite migration history.
- Inspect every newly generated migration before accepting it and confirm that it contains only the schema changes required by the current feature.
- Applying migrations to a database, dropping a database, or resetting database data requires explicit authorization from the repository owner.

### Testing and Verification

- Do not recreate the deleted `AIStudyHub.Tests` project.
- Do not create unit-test projects, unit-test files, test fixtures, mocks, test packages, or test-only production hooks when adding or fixing features.
- Do not add xUnit, NUnit, MSTest, Moq, FluentAssertions, or equivalent unit-testing dependencies unless the repository owner explicitly reverses this policy.
- Create integration or end-to-end test projects only when the repository owner explicitly requests them.
- Do not create, run, require, or recommend smoke tests.
- Agents may run `dotnet build` to verify that the solution compiles.
- Functional verification is performed manually by the repository owner.
- Every feature handoff must list the manual flows that the repository owner should verify.

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
- **Real-time notification service**: singleton (wraps `IHubContext<NotificationsHub, INotificationsHub>` which is itself singleton).
- **Gamification / FlashcardReview / Recommendation services**: scoped (depend on scoped repositories).
- **SignalR Hub class**: transient (per-connection).

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
   - Accepts uploads only after file and `Processing` document persistence; content above exactly 5,242,880 bytes is rejected with `413 Payload Too Large`.
   - Reads from `IDocumentProcessingQueue` (bounded in-process dispatch queue) and recovers active `Processing` documents from persistence at startup.
   - Processes: text extraction, Kernel Memory import, embedding, Qdrant upsert.
   - Updates document status to `Done` or `Failed`; recovery marks the document `Failed` when its stored source file is missing.
   - Graceful error handling per job.
   - On completion, pushes `ReceiveNotification` with `type=Document` so FE can show "Document ready".

2. **TierExpirationCleanupService** (`BackgroundService`)
   - Runs every `TierExpirationCheckIntervalHours` (default 24h).
   - Finds users where `TierExpireAt < UtcNow` and not on Free tier.
   - Downgrades to Free tier, clears expiration date.

3. **UnverifiedAccountCleanupService** (`BackgroundService`)
   - Runs daily at midnight UTC.
   - Finds users where `!EmailConfirmed && CreatedAt < cutoffDate` (default 7 days).
   - Cascades deletes: OtpRecords, Qdrant vectors, files, DocumentChunks, Documents, Flashcards, UserRoles, Notifications, User.

4. **DailyStreakResetWorker** (`BackgroundService`)
   - Runs daily (configurable schedule).
   - For every `UserStats` row where `LastActivityDate < UtcNow.Date.AddDays(-1)`, sets `CurrentStreak = 0`.
   - Best streak is preserved.
   - When a user is about to lose their streak (last activity 23h ago, end-of-day approaching), a one-time `StreakAtRisk` notification is pushed via SignalR.

## Future Scalability

Recommended evolution paths:

- Add caching (Redis) for frequently accessed public document metadata.
- Add health checks for SQL Server and Qdrant.
- Add rate limiting on AI and upload endpoints.
- Add API versioning before public clients depend on the API.
- Add observability with metrics (Prometheus) and distributed tracing.
- Add object storage (Azure Blob / S3) for production file storage.
- Add audit logging for admin and payment actions.
- Add an external message queue (RabbitMQ / Azure Queue) for multi-instance dispatch and coordination.
- Replace the public `/api/Gamification/award-xp` endpoint with a server-to-server auth scheme (mTLS or shared HMAC) before going to production.
- Split AI provider implementations behind interfaces for multi-provider support.
- Keep the current 3-layer architecture unless scaling requirements justify a larger architecture.
