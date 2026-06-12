# AI Study Hub

AI Study Hub is an ASP.NET Core 8 Web API for AI-assisted learning document management. The system is designed around a clean MVC 3-layer architecture with SQL Server persistence, JWT authentication, Swagger documentation, validation, mapping, logging, repositories, and Unit of Work.

## Tech Stack

- ASP.NET Core 8 Web API
- SQL Server
- Entity Framework Core 8 Code First
- Swagger / OpenAPI
- JWT Authentication
- AutoMapper
- FluentValidation
- Serilog
- Repository Pattern
- Unit of Work Pattern

## Architecture

The solution uses exactly 3 layers:

```text
Client
→ AIStudyHub.API
→ AIStudyHub.Business
→ AIStudyHub.Data
→ SQL Server
```

Projects:

- `AIStudyHub.API`: Presentation layer, controllers, middleware, Swagger, JWT, API dependency registration.
- `AIStudyHub.Business`: Business layer, entities, DTOs, enums, service contracts, services, validators, mappings.
- `AIStudyHub.Data`: Data access layer, DbContext, repositories, Unit of Work, EF configurations, migrations, seed data.

## Main Modules

- Authentication
- User Management
- Document Management
- Document Search
- Document Voting
- Document Reporting
- AI Chat
- Flashcard Generation
- Quiz Generation
- Quiz Submission
- Notification
- Payment
- Admin

## Solution Structure

```text
AIStudyHub.slnx
├── AIStudyHub.API
├── AIStudyHub.Business
├── AIStudyHub.Data
├── docs
│   ├── AGENT.md
│   └── ARCHITECTURE.md
├── .gitignore
└── README.md
```

## Prerequisites

- .NET 8 SDK
- SQL Server
- Visual Studio 2022, Rider, or VS Code
- EF Core CLI tools

Install EF Core tools if needed:

```bash
dotnet tool install --global dotnet-ef
```

## Configuration

Runtime settings are intentionally not committed.

Create your local configuration file from the example:

```bash
copy AIStudyHub.API\appsettings.example.json AIStudyHub.API\appsettings.json
```

Default connection string in the example file:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AIStudyHub;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

JWT settings are stored under the `Jwt` section in your local `AIStudyHub.API/appsettings.json`.

Do not commit real environment configuration. Use user secrets, environment variables, or a secure secret store for production values.

## Getting Started

Restore dependencies:

```bash
dotnet restore AIStudyHub.slnx
```

Build the solution:

```bash
dotnet build AIStudyHub.slnx
```

Run the API:

```bash
dotnet run --project AIStudyHub.API
```

Open Swagger:

```text
https://localhost:{port}/swagger
```

or, when running with a fixed URL:

```bash
dotnet run --project AIStudyHub.API --urls http://localhost:5000
```

Swagger:

```text
http://localhost:5000/swagger
```

## Database Migrations

Create a migration:

```bash
dotnet ef migrations add InitialCreate --project AIStudyHub.Data --startup-project AIStudyHub.API
```

Apply migrations:

```bash
dotnet ef database update --project AIStudyHub.Data --startup-project AIStudyHub.API
```

## API Documentation

Swagger is configured in the API project and enabled in development mode.

Main API groups:

- `/api/Auth`
- `/api/User`
- `/api/Document`
- `/api/Quiz`
- `/api/Flashcard`
- `/api/Vote`
- `/api/Report`
- `/api/Notification`
- `/api/Payment`
- `/api/Chat`
- `/api/Admin`

## Database Entity Model (ER Diagram)

The database schema is based on Entity Framework Code First. The core tables and their relationships are represented below:

```mermaid
erDiagram
    User ||--o{ Document : "owns"
    User ||--o{ Vote : "casts"
    User ||--o{ Report : "creates"
    User ||--o{ Notification : "receives"
    User ||--o{ Payment : "makes"
    User ||--o{ QuizSubmission : "submits"
    User ||--o{ ChatSession : "initiates"
    User ||--o{ RefreshToken : "has"
    User ||--o{ TierUser : "belongs_to"
    User ||--o{ OtpRecord : "has"
    
    Subject ||--o{ Document : "categorizes"
    
    TierMembership ||--o{ TierUser : "includes"
    TierMembership ||--o{ Payment : "associated_with"
    
    Document ||--o{ Vote : "receives"
    Document ||--o{ Report : "receives"
    Document ||--o{ Flashcard : "has"
    Document ||--o{ Quiz : "has"
    Document ||--o{ DocumentChunk : "divided_into"
    Document ||--o{ ChatSession : "discusses"
    
    Quiz ||--o{ Question : "contains"
    Quiz ||--o{ QuizSubmission : "receives"
    
    Question ||--o{ Answer : "has"
    
    ChatSession ||--o{ ChatMessage : "contains"
    
    User {
        Guid Id PK
        string FullName
        DateOnly DateOfBirth
        int CurrentStorageCapacity
        int CurrentAiTokenUsage
        string Status
        string Role
        bool IsActive
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    Subject {
        Guid Id PK
        string SubjectCode
        string SubjectName
        string Description
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    Document {
        Guid Id PK
        Guid UserId FK
        Guid SubjectId FK
        string Title
        string FileLink
        string FileName
        string FileExtension
        string FileType
        string SharedUsers
        string ShareStatus
        DocumentStatus Status
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    DocumentChunk {
        Guid Id PK
        Guid DocumentId FK
        string ChunkJson
        string EmbeddingJson
        string VectorId
        int OrderIndex
        byte[] Vector
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    Vote {
        Guid Id PK
        Guid UserId FK
        Guid DocumentId FK
        VoteType Type
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    Report {
        Guid Id PK
        Guid UserId FK
        Guid DocumentId FK
        string Reason
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    Flashcard {
        Guid Id PK
        Guid DocumentId FK
        string Front
        string Back
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    Quiz {
        Guid Id PK
        Guid DocumentId FK
        string Title
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    Question {
        Guid Id PK
        Guid QuizId FK
        string Title
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    Answer {
        Guid Id PK
        Guid QuestionId FK
        string SelectedOption
        bool IsCorrect
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    QuizSubmission {
        Guid Id PK
        Guid UserId FK
        Guid QuizId FK
        string Answers
        decimal Score
        DateTime SubmittedAt
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    ChatSession {
        Guid Id PK
        Guid UserId FK
        Guid DocumentId FK
        string SessionTitle
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    ChatMessage {
        Guid Id PK
        Guid ChatSessionId FK
        string Sender
        string Content
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    Notification {
        Guid Id PK
        Guid UserId FK
        string Message
        bool IsRead
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    Payment {
        Guid Id PK
        Guid UserId FK
        Guid TierId FK
        string PaymentInfo
        DateTime PaymentDate
        PaymentStatus Status
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    TierMembership {
        Guid Id PK
        string TierName
        int StorageLimitMb
        int AiTokens
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    TierUser {
        Guid Id PK
        Guid UserId FK
        Guid TierMembershipId FK
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    RefreshToken {
        Guid Id PK
        Guid UserId FK
        string TokenHash
        DateTime ExpiresAt
        DateTime RevokedAt
        string ReplacedByTokenHash
        DateTime CreatedAt
        DateTime UpdatedAt
    }
    
    OtpRecord {
        Guid Id PK
        Guid UserId FK
        string Email
        string OtpHash
        OtpType Type
        DateTime ExpiresAt
        DateTime UsedAt
        int FailedAttempts
        DateTime LockedUntil
        DateTime CreatedAt
        DateTime UpdatedAt
    }
```

## Security Notes

- JWT Bearer authentication is configured.
- Admin endpoints should use role-based authorization.
- Passwords must be hashed before persistence when authentication logic is implemented.
- Do not expose `PasswordHash` or sensitive fields in DTOs.
- Do not commit real JWT secrets, certificates, payment credentials, or production connection strings.

## Logging

Serilog is configured for:

- Console logging
- Rolling file logs under `logs/`
- Structured request logging
- Global exception logging

Runtime logs are ignored by Git.

## Documentation

Additional project documentation:

- [Agent Guide](docs/AGENT.md)
- [Architecture Reference](docs/ARCHITECTURE.md)

## Current Status

This repository currently contains a production-ready skeleton architecture. Business logic is intentionally not implemented yet.

Service methods are placeholders and should be completed module by module.

## Future Work

- Implement authentication and JWT token generation.
- Add password hashing.
- Implement document upload and storage.
- Implement document search.
- Add AI provider integration for chat, flashcards, and quizzes.
- Add payment gateway integration and webhook verification.
- Generate real EF Core migrations.
- Add unit and integration tests.
- Add rate limiting and health checks.
