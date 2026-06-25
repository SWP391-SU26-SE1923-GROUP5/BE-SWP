# AI Study Hub

AI Study Hub is a production-ready ASP.NET Core 8 Web API for AI-assisted learning document management. The system uses a clean 3-layer architecture (API / Business / Data), SQL Server persistence, JWT authentication, and a full RAG pipeline powered by local AI models.

## Tech Stack

| Category | Technology |
|----------|-----------|
| Framework | ASP.NET Core 8 Web API |
| Database | SQL Server + Entity Framework Core 8 Code First |
| AI - Embedding | OpenAI SDK (`EmbeddingClient`) or Ollama (`nomic-embed-text`) |
| AI - Vector Store | Qdrant (local vector DB) |
| AI - LLM Orchestration | Semantic Kernel + Microsoft Kernel Memory |
| AI - Generators | OpenAI SDK (`ChatClient`) or Ollama LLM (quiz & flashcard generation) |
| AI - Search | Hybrid search (dense embeddings + BM25 sparse, reranked with Reciprocal Rank Fusion) |
| AI - Guardrails | Faithfulness filter, grounding verifier, confidence scorer |
| Authentication | JWT Bearer + Refresh Tokens + OTP (email verification / password reset) |
| External Auth | Google OAuth, GitHub OAuth |
| ORM Mapping | AutoMapper |
| Validation | FluentValidation (with global action filter) |
| Logging | Serilog (console + rolling file) |
| Payment | VNPay gateway |
| Background Jobs | `BackgroundService` (document processing queue, tier cleanup, unverified-account cleanup) |
| Patterns | Repository + Unit of Work, CQRS via MediatR (Auth, Users) |

## Architecture

Three layers with strict separation of concerns:

```text
Client
  -> AIStudyHub.API        (Controllers, Middleware, JWT, Swagger, DI registration)
  -> AIStudyHub.Business   (Services, AI pipeline, DTOs, Validators, Mappings)
  -> AIStudyHub.Data       (EF Core DbContext, Repositories, UnitOfWork, Migrations, Seed)
  -> SQL Server
```

## Project Overview

### AIStudyHub.API
Presentation layer. Hosts 18 REST controllers, configures middleware pipeline, JWT + OAuth authentication, rate limiting, Serilog, Swagger/OpenAPI, and static file serving.

### AIStudyHub.Business
Business layer. Contains the entire AI pipeline (L3-L5), 14+ business services, 16 FluentValidation modules, AutoMapper profiles, MediatR CQRS handlers for Auth and Users, and 3 background hosted services.

### AIStudyHub.Data
Data access layer. Holds 18 entity classes (all inheriting `BaseEntity`), 7 enums, Fluent API configurations, generic Repository + UnitOfWork, and 15 EF Core migrations.

## Main Modules

| Module | Description |
|--------|-------------|
| **Authentication** | Register with email OTP verification, login with JWT + refresh tokens, password reset, Google/GitHub OAuth, logout |
| **User Management** | CRUD, profile update, tier info, document sharing |
| **Document Management** | Upload (multipart/form-data), text extraction (PDF/DOCX/TXT/MD), chunking, versioning, sharing, vote/report |
| **RAG Pipeline** | Hybrid search (dense + sparse BM25 via Qdrant RRF), reranking, Semantic Kernel orchestration, guardrails (faithfulness, grounding, confidence), document Q&A, summarization |
| **Flashcard Generation** | AI-powered flashcard creation from document chunks, auto-persisted |
| **Quiz Generation** | AI-powered multiple-choice quiz creation, auto-persisted with questions and answers |
| **Quiz Submission** | Submit answers, auto-grading, score tracking |
| **AI Chat** | Session-based chat with document context, RAG-augmented responses |
| **Notifications** | System and feature-driven notifications |
| **Payments** | VNPay checkout, webhook processing, tier upgrade on success |
| **Tier Memberships** | Subscription tiers with storage and AI token quotas |
| **Admin** | Dashboard data, document reindexing, user/document/report moderation |

## AI Architecture

See full sequence diagrams in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). Key flows:

#### L1 — Document Ingestion Pipeline

```mermaid
sequenceDiagram
    participant Client
    participant Ctrl as DocumentUploadController
    participant Storage as LocalFileStorageService
    participant DocSvc as DocumentService
    participant Queue as DocumentProcessingQueue
    participant Proc as DocumentBackgroundProcessor
    participant KM as KernelMemoryService
    participant Embed as EmbeddingService
    participant Sparse as Bm25SparseGenerator
    participant Qdrant as QdrantVectorService
    participant DB as DbContext

    Client->>Ctrl: POST /api/Document/upload (multipart/form-data)
    Ctrl->>Storage: SaveFileAsync(file)
    Storage-->>Ctrl: filePath
    Ctrl->>DocSvc: CreateAsync(dto)
    DocSvc->>DB: Insert Document (Status=Processing)
    DocSvc-->>Ctrl: documentId
    Ctrl->>Queue: EnqueueAsync(request)
    Ctrl-->>Client: 202 Accepted

    par Async Background Processing
        Proc->>KM: ImportDocumentAsync(filePath, docId, userId, fileName)
        Note over KM: PdfPig / OpenXML → raw text
        Note over KM: Sentence-split chunking + overlapping
        Note over KM: Tags: user_id, file_name

        Proc->>KM: SearchAsync("", userId, limit=1000)
        Proc->>Embed: GenerateEmbeddingAsync(chunk)
        Note over Embed: OpenAI SDK → float[] dense vector
        Proc->>Sparse: GenerateSparseVector(chunk)
        Note over Sparse: FNV-1a word hashing + sub-linear TF
        Proc->>Qdrant: UpsertVectorAsync(dense, sparse, metadata)
        Note over Qdrant: Stores: "" dense + "sparse-text" named vector

        Proc->>DB: Update Document (Status=Published)
    end
```

#### L2-L5 — RAG Query Flow (Chat with Document)

```mermaid
sequenceDiagram
    participant Client
    participant Ctrl as RagController
    participant Orch as SemanticKernelOrchestrator
    participant Hybrid as HybridSearchService
    participant Embed as EmbeddingService
    participant Sparse as Bm25SparseGenerator
    participant Qdrant as QdrantVectorService
    participant Rerank as RerankingService
    participant LLM as LocalAIService
    participant Faith as FaithfulnessFilter
    participant Ground as GroundingVerifier
    participant Score as ConfidenceScorer

    Client->>Ctrl: POST /api/Rag/chat { question }
    Ctrl->>Orch: AskAsync(userId, question)

    rect rgb(235, 245, 255)
        Note over Orch,Qdrant: L2 — Retrieval

        Orch->>Hybrid: SearchAsync(question, userId, topK=10)
        par
            Hybrid->>Embed: GenerateEmbeddingAsync(question)
            Hybrid->>Sparse: GenerateSparseVector(question)
        end
        Hybrid->>Qdrant: HybridSearchAsync(dense, sparse, topK=20, filter)
        Note over Qdrant: prefetch[0]: dense + prefetch[1]: sparse<br/>query.fusion = "rrf"
        Hybrid-->>Orch: List<SearchResult> (20 items)
        Orch->>Rerank: RerankAsync(question, results, topK=5)
        Note over Rerank: Score × (1.0 - index × 0.1)
        Rerank-->>Orch: List<SearchResult> (5 items)
    end

    rect rgb(240, 255, 240)
        Note over Orch,LLM: L3 — Generation

        Orch->>Orch: Build context + system prompt + user prompt
        Orch->>LLM: SendMessageAsync(systemPrompt + userPrompt)
        Note over LLM: OpenAI Chat Completions API
        LLM-->>Orch: answer
    end

    rect rgb(255, 245, 230)
        Note over Orch,Score: L4 — Guardrails

        Orch->>Faith: ValidateAsync(answer, sources)
        Note over Faith: Detects evasive phrases
        Orch->>Ground: VerifyAsync(answer, sources)
        Note over Ground: Word-overlap grounding score
        Orch->>Score: Score(answer, grounding, isFaithful)
        Note over Score: Combined + clamped [0,1]
    end

    Orch-->>Ctrl: RagResponse(answer, citations, confidence)
    Ctrl-->>Client: 200 OK
```

#### L6 — Flashcard Generation

```mermaid
sequenceDiagram
    participant Client
    participant Ctrl as FlashcardController
    participant Svc as FlashcardAiService
    participant KM as KernelMemoryService
    participant LLM as LocalAIService
    participant DB as DbContext

    Client->>Ctrl: POST /api/Flashcard/generate-ai { numberOfFlashcards }
    Ctrl->>Svc: GenerateFlashcardsAsync(docId, request, userId)
    Svc->>KM: SearchAsync("", filter=documentId, limit=1000)
    KM-->>Svc: MemoryAnswer.Results[]
    Svc->>Svc: BuildContext(citations) — max 30k chars

    loop Batch (batchSize=5, maxAttempts=80)
        Svc->>LLM: SendMessageAsync(batchPrompt, temp=0.2)
        Note over LLM: Extract N facts → JSON flashcards
        LLM-->>Svc: aiText (raw JSON)
        Svc->>Svc: ParseFlashcardArray (3-stage parser)<br/>Normalize + dedupe + enforce front/back rules
    end

    Svc->>DB: Insert Flashcard entities
    Svc-->>Ctrl: FlashcardResponseDto[]
    Ctrl-->>Client: 200 OK
```

#### L6 — Quiz Generation

```mermaid
sequenceDiagram
    participant Client
    participant Ctrl as QuizController
    participant Svc as QuizAiService
    participant Qdrant as QdrantVectorService
    participant LLM as LocalAIService
    participant DB as DbContext

    Client->>Ctrl: POST /api/Quiz/generate-ai { numberOfQuestions }
    Ctrl->>Svc: GenerateAndPersistQuizAsync(docId, request, userId)
    Svc->>Qdrant: GetPayloadsByDocumentIdAsync(documentId)
    Note over Qdrant: REST scroll API → all chunks
    Qdrant-->>Svc: List<Dictionary<string,string>>
    Svc->>Svc: Sort by chunkIndex + FixMojibake<br/>Concatenate chunks as context

    loop Batch (batchSize=3, maxAttempts=60)
        Svc->>LLM: SendMessageAsync(batchPrompt, temp=0.2)
        Note over LLM: Read TEXT → JSON quiz<br/>N questions × 4 answers, 1 correct
        LLM-->>Svc: aiText (raw JSON)
        Svc->>Svc: ParseQuizPayload (3-stage parser)<br/>Normalize + dedupe + enforce SingleChoice
    end

    Svc->>DB: Insert Quiz → Questions → Answers
    Svc-->>Ctrl: QuizResponseDto
    Ctrl-->>Client: 200 OK
```

### Local AI Requirements

| Model | Purpose |
|-------|---------|
| `text-embedding-3-small` | Text embeddings (1536 dimensions via OpenAI SDK) |
| `gpt-4o-mini` (or custom) | LLM chat completions and content generation |

**Vector DB:** Qdrant at `http://localhost:6333` with hybrid (dense + sparse named vector `sparse-text`) collection.

## Solution Structure

```text
AIStudyHub.slnx
├── AIStudyHub.API
│   ├── Controllers/          (18 controllers)
│   ├── DTOs/
│   ├── Extensions/           (JWT, Swagger, Rate Limiting)
│   ├── Middleware/           (GlobalException, FluentValidation filter)
│   ├── Swagger/
│   ├── Program.cs
│   └── appsettings.json
├── AIStudyHub.Business
│   ├── AI/
│   │   ├── Chat/             (AIChatService)
│   │   ├── Generators/       (QuizAiService, FlashcardAiService)
│   │   ├── Guardrails/       (Faithfulness, Grounding, Confidence)
│   │   ├── LLM/              (LocalAIService)
│   │   ├── Orchestration/    (SemanticKernelOrchestrator, KernelMemoryService)
│   │   ├── Search/           (HybridSearch, Reranking, BM25 sparse)
│   │   └── VectorStore/      (QdrantVectorService, EmbeddingService)
│   ├── Behaviors/            (MediatR pipeline behaviors)
│   ├── Configuration/        (Retrieval, KernelMemory, SemanticKernel, Guardrails options)
│   ├── DTOs/                 (16 module DTOs)
│   ├── Features/             (MediatR CQRS - Auth, Users)
│   ├── Interfaces/           (AI + Service interfaces)
│   ├── Mappings/             (AutoMapper profiles)
│   ├── Options/              (JWT, SMTP, VnPay, Rag, etc.)
│   ├── Services/             (Business services + VnPay + Email + FileStorage)
│   ├── Validators/           (16 FluentValidation modules)
│   └── Workers/              (DocumentProcessor, TierCleanup, UnverifiedCleanup)
├── AIStudyHub.Data
│   ├── Configurations/       (EF Core Fluent API for 16 entities)
│   ├── Entities/             (18 entities + BaseEntity)
│   ├── Enums/                (7 enums)
│   ├── Extensions/           (DataAccess DI, AdminSeed)
│   ├── Interfaces/            (Repository + UnitOfWork interfaces)
│   ├── Repositories/         (GenericRepository, UnitOfWork)
│   └── Migrations/           (15 EF Core migrations)
├── AIStudyHub.Tests/
├── docs/
│   ├── AGENT.md              (Coding rules & conventions)
│   ├── ARCHITECTURE.md        (Architecture deep-dive)
│   ├── NET8_3_LAYER_GUIDE.md  (Implementation guide)
│   └── EF_MIGRATION_COMMANDS.md
├── .github/workflows/
├── .gitignore
└── README.md
```

## Database Entities (18)

All entities inherit from `BaseEntity` (Id: Guid, CreatedAt, UpdatedAt), except `User` which inherits `IdentityUser<Guid>` (has additional Identity columns: NormalizedEmail, NormalizedUserName, PasswordHash, SecurityStamp, ConcurrencyStamp, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount).

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

## API Controllers (18)

| Controller | Route | Description |
|------------|-------|-------------|
| `AuthController` | `/api/Auth` | Register, login, OTP, OAuth, JWT refresh |
| `UserController` | `/api/User` | Profile, tier, sharing |
| `DocumentController` | `/api/Document` | Metadata CRUD, sharing, download |
| `DocumentUploadController` | `/api/DocumentUpload` | File upload, processing queue, chunk retrieval |
| `ChatController` | `/api/Chat` | Sessions and messages |
| `FlashcardController` | `/api/Flashcard` | CRUD + AI generation |
| `QuizController` | `/api/Quiz` | CRUD + AI generation |
| `QuestionController` | `/api/Question` | Question CRUD |
| `AnswerController` | `/api/Answer` | Answer CRUD |
| `QuizSubmissionController` | `/api/QuizSubmission` | Submission results |
| `VoteController` | `/api/Vote` | Upvote/downvote documents |
| `ReportController` | `/api/Report` | Document violation reports |
| `NotificationController` | `/api/Notification` | User notifications |
| `SubjectController` | `/api/Subject` | Academic subjects |
| `PaymentController` | `/api/Payment` | VNPay checkout and webhook |
| `TierMembershipController` | `/api/TierMembership` | Subscription tiers |
| `RagController` | `/api/Rag` | RAG query and summarization |
| `AdminController` | `/api/Admin` | Reindexing and moderation |

## Background Workers

1. **DocumentBackgroundProcessor** — dequeues uploaded documents, extracts text, chunks, embeds via Ollama, upserts to Qdrant, updates DB status
2. **TierExpirationCleanupService** — runs every 24h, downgrades expired subscriptions to Free tier
3. **UnverifiedAccountCleanupService** — runs daily, removes accounts older than 7 days that are still unverified

## Prerequisites

- .NET 8 SDK
- SQL Server
- [Ollama](https://ollama.com) running locally with `nomic-embed-text` and `llama3.2:3b` models
- [Qdrant](https://qdrant.tech) running locally (`docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant`)
- EF Core CLI tools (optional)

## Configuration

Create `AIStudyHub.API/appsettings.json` from the example. Key sections:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AIStudyHub;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Issuer": "AIStudyHub",
    "Audience": "AIStudyHub.Client",
    "SecretKey": "CHANGE_THIS",
    "ExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 7
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text",
    "GenerationModel": "llama3.2:3b"
  },
  "Qdrant": {
    "Url": "http://localhost:6333",
    "CollectionName": "aistudyhub-docs",
    "VectorSize": 768
  },
  "Smtp": {
    "Host": "smtp.example.com",
    "Port": 587,
    "UserName": "...",
    "Password": "..."
  },
  "VnPay": {
    "TmnCode": "...",
    "HashSecret": "...",
    "BaseUrl": "https://sandbox.vnpayment.vn",
    "ReturnUrl": "https://yourdomain.com/api/Payment/vnpay-return"
  },
  "ExternalAuth": {
    "Google": { "ClientId": "...", "ClientSecret": "..." },
    "GitHub": { "ClientId": "...", "ClientSecret": "..." }
  }
}
```

**Do not commit real credentials.** Use user secrets, environment variables, or a secure secret store.

## Getting Started

```bash
# Restore and build
dotnet build AIStudyHub.slnx

# Run the API
dotnet run --project AIStudyHub.API

# Swagger UI
https://localhost:{port}/swagger
```

## Database Migrations

```bash
# Add a migration
dotnet ef migrations add <Name> --project AIStudyHub.Data --startup-project AIStudyHub.API

# Apply to database
dotnet ef database update --project AIStudyHub.Data --startup-project AIStudyHub.API
```

## Security Notes

- JWT Bearer authentication on all protected endpoints
- Admin endpoints require `[Authorize(Roles = "Admin")]`
- OTP-based email verification for registration and password reset
- Ownership checks in services for student-owned resources
- VNPay webhook signature validation
- Passwords hashed via ASP.NET Identity
- Do not expose `PasswordHash`, OTP codes, JWT secrets, or payment credentials in responses or logs

## Logging

Serilog configured for console and rolling file output (`logs/`). Structured logging throughout. Runtime logs ignored by Git.

## Documentation

- [Agent Guide](docs/AGENT.md) — coding conventions and rules
- [Architecture Reference](docs/ARCHITECTURE.md) — architecture deep-dive with diagrams
- [.NET 8 3-Layer Guide](docs/NET8_3_LAYER_GUIDE.md) — implementation template and patterns
- [EF Migration Commands](docs/EF_MIGRATION_COMMANDS.md) — database migration cheatsheet

## Status

Fully implemented production-ready backend. All core modules are wired up and functional.
