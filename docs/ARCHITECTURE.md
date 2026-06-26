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

`DocumentStatus` (Draft/Published/Archived/Banned/Processing/Failed), `NotificationType`, `PaymentStatus`, `QuestionType` (SingleChoice/MultipleChoice/TrueFalse), `UserRole` (Student/Admin), `ReportStatus`, `VoteType` (Upvote/Downvote).

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

### RAG & AI Generation Flows

#### L1 — Document Ingestion Pipeline

```mermaid
sequenceDiagram
    participant Client
    participant Controller as DocumentUploadController
    participant Storage as LocalFileStorageService
    participant DocSvc as DocumentService
    participant Queue as DocumentProcessingQueue
    participant Processor as DocumentBackgroundProcessor
    participant KM as KernelMemoryService
    participant EmbedSvc as EmbeddingService
    participant SparseGen as Bm25SparseGenerator
    participant Qdrant as QdrantVectorService
    participant DB as DbContext

    Client->>Controller: POST /api/Document/upload (multipart/form-data)
    Controller->>Storage: SaveFileAsync(file)
    Storage-->>Controller: filePath

    Controller->>DocSvc: CreateAsync(dto)
    DocSvc->>DB: Insert Document (Status=Processing)
    DocSvc-->>Controller: documentId

    Controller->>Queue: EnqueueAsync(request)
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

#### L2-L5 — RAG Query Flow (Chat with Document)

```mermaid
sequenceDiagram
    participant Client
    participant Controller as RagController
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

    Client->>Controller: POST /api/Rag/chat { question, documentIds? }
    Controller->>Orch: AskAsync(userId, question)

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

    Client->>Controller: POST /api/Flashcard/generate-ai { numberOfFlashcards }
    Controller->>FlashSvc: GenerateFlashcardsAsync(docId, request, userId)

    FlashSvc->>FlashSvc: Validate document ownership

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

    FlashSvc->>DB: Insert Flashcard entities
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

    Client->>Controller: POST /api/Quiz/generate-ai { numberOfQuestions }
    Controller->>QuizSvc: GenerateAndPersistQuizAsync(docId, request, userId)

    QuizSvc->>QuizSvc: Validate document + ownership

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
    QuizSvc-->>Controller: QuizResponseDto
    Controller-->>Client: 200 OK
```

### AI Components

| Component | Implementation | Purpose |
|-----------|---------------|---------|
| `ILocalAIService` | `LocalAIService` | Chat completion + embeddings via OpenAI SDK (`ChatClient`, `EmbeddingClient`) |
| `IEmbeddingService` | `EmbeddingService` | Wraps `ILocalAIService.CreateEmbeddingsFromTexts` for dense vector generation |
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
| `IDocumentProcessingQueue` | `DocumentProcessingQueue` | In-memory channel-based async job queue for document processing |
| `DocumentBackgroundProcessor` | `DocumentBackgroundProcessor` | `BackgroundService` — dequeues jobs, calls KernelMemory import + generates dense/sparse vectors → Qdrant |

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
- Add health checks for SQL Server and Qdrant.
- Add rate limiting on AI and upload endpoints.
- Add API versioning before public clients depend on the API.
- Add observability with metrics (Prometheus) and distributed tracing.
- Add object storage (Azure Blob / S3) for production file storage.
- Add audit logging for admin and payment actions.
- Add message queue (RabbitMQ / Azure Queue) for resilient background processing.
- Split AI provider implementations behind interfaces for multi-provider support.
- Keep the current 3-layer architecture unless scaling requirements justify a larger architecture.
