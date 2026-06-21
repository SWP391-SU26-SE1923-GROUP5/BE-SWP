# Project Overview

AI Study Hub is an ASP.NET Core 8 Web API for AI-assisted learning document management.

Architecture: MVC 3-layer architecture.

Projects:

- `AIStudyHub.API`: Presentation Layer
- `AIStudyHub.Business`: Business Layer
- `AIStudyHub.Data`: Data Access Layer

Technology stack:

- ASP.NET Core 8 Web API
- SQL Server
- Entity Framework Core 8 Code First
- Swagger/OpenAPI
- JWT Authentication
- AutoMapper
- FluentValidation
- Serilog
- Repository Pattern
- Unit of Work Pattern

# Business Goals

- Allow students to upload, manage, search, and study learning documents.
- Use AI-assisted features to create flashcards, quizzes, and chat-based study support.
- Support community quality signals through voting and reporting.
- Provide notifications and payment-ready architecture.
- Keep API architecture maintainable, testable, and production-ready.

# Main Features

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

# User Roles

Guest:

- Can register and login.
- Can access public API documentation in development.
- Must not access protected resources.

Student:

- Can manage own profile.
- Can upload and manage own documents.
- Can vote and report documents.
- Can use AI chat, flashcards, quizzes, submissions, notifications, and payments.

Admin:

- Can access admin APIs.
- Can moderate users, documents, reports, and system data.
- Can review platform-wide activity.

# Coding Rules

- Keep exactly 3 layers: API, Business, Data.
- Do not put EF Core or database logic in `AIStudyHub.API`.
- Do not put controller or HTTP-specific logic in `AIStudyHub.Business`.
- Do not put business rules in `AIStudyHub.Data`.
- Use dependency injection for all services and repositories.
- Keep methods async when they perform I/O.
- Use `CancellationToken` in public async APIs.
- Avoid business logic in controllers.
- Keep controllers thin: validate request, call service, return response.
- **NEVER use generic base controllers (like `CrudControllerBase`)**. Controllers must explicitly define only the endpoints required by business rules (e.g., Vote only needs POST and DELETE, no PUT).
- Do not introduce additional projects without explicit instruction.

# Naming Conventions

- Use PascalCase for classes, methods, properties, DTOs, enums, and files.
- Use camelCase for local variables and parameters.
- Interfaces must start with `I`.
- DTO names must end with `Dto`.
- Request DTOs should use `Create`, `Update`, `Login`, or `Register` prefixes where appropriate.
- Response DTOs should end with `ResponseDto`.
- Validators must end with `Validator`.
- Entity configuration classes must end with `Configuration`.
- Services must end with `Service`.
- Repositories must end with `Repository`.

# Folder Conventions

`AIStudyHub.API`:

- `Controllers/`
- `Middleware/`
- `Extensions/`
- `Program.cs`
- `appsettings.json`

`AIStudyHub.Business`:

- `AI/`
- `Entities/`
- `DTOs/{ModuleName}/`
- `Enums/`
- `Interfaces/AI/`
- `Interfaces/Services/`
- `Services/`
- `Validators/{ModuleName}/`
- `Mappings/`

`AIStudyHub.Data`:

- `ApplicationDbContext.cs`
- `Configurations/`
- `Interfaces/`
- `Repositories/`
- `Extensions/`
- `Seed/`
- `Migrations/`

# Entity Relationships Summary

- User 1-N Documents
- User 1-N Votes
- User 1-N Reports
- User 1-N Notifications
- User 1-N Payments
- User 1-N QuizSubmissions
- User 1-N ChatSessions
- Document 1-N Votes
- Document 1-N Reports
- Document 1-N Flashcards
- Document 1-N Quizzes
- Quiz 1-N Questions
- Question 1-N Answers
- Quiz 1-N QuizSubmissions
- ChatSession 1-N ChatMessages

Core entities:

- `User`
- `Document`
- `Vote`
- `Report`
- `Flashcard`
- `Quiz`
- `Question`
- `Answer`
- `QuizSubmission`
- `Notification`
- `Payment`
- `ChatSession`
- `ChatMessage`

# API Design Rules

- Use REST-style routes under `/api/{Controller}`.
- Use plural or module-aligned resource semantics consistently.
- Use `[ApiController]`.
- Use `[Authorize]` for protected endpoints.
- Use `[Authorize(Roles = "Admin")]` for admin-only endpoints.
- Return DTOs, never entities.
- Use proper status codes:
  - `200 OK` for successful queries and updates.
  - `201 Created` for successful creates.
  - `204 NoContent` for successful deletes.
  - `400 BadRequest` for validation failures.
  - `401 Unauthorized` for missing/invalid authentication.
  - `403 Forbidden` for insufficient permissions.
  - `404 NotFound` for missing resources.
  - `500 InternalServerError` only for unexpected failures.
- Keep Swagger enabled for development.

# Database Rules

- Use SQL Server.
- Use EF Core 8 Code First.
- Use Fluent API configurations in `AIStudyHub.Data/Configurations`.
- Do not rely on data annotations for schema design unless already established.
- Every entity must inherit from `BaseEntity`.
- Every entity must include:
  - `Id`
  - `CreatedAt`
  - `UpdatedAt`
- Configure relationships explicitly.
- Configure string max lengths.
- Configure decimal precision.
- Use enum-to-string conversions for readable database values.
- Use migrations for schema changes.
- Put seed data in `Seed/SeedData.cs`.

# Service Layer Rules

- Services live in `AIStudyHub.Business/Services`.
- Service contracts live in `AIStudyHub.Business/Interfaces/Services`.
- For complex domains (like Auth and Users), use the **CQRS pattern with MediatR** (located in `AIStudyHub.Business/Features`).
- For standard CRUD domains, use standard Service classes.
- Services contain business rules and orchestration.
- Services should depend on abstractions, not concrete data access classes.
- Services should return DTOs.
- Services should not expose `IQueryable`.
- Services should not reference ASP.NET Core HTTP types.
- Keep authentication, authorization decisions, and ownership checks explicit.

# Repository Layer Rules

- Repositories live in `AIStudyHub.Data/Repositories`.
- Repository interfaces live in `AIStudyHub.Data/Interfaces`.
- Use `GenericRepository<TEntity>` for basic CRUD access.
- Use `IUnitOfWork` for transaction boundaries and coordinated persistence.
- Repositories should work with entities, not DTOs.
- Repositories should not contain business rules.
- Avoid leaking EF Core tracking behavior unless intentionally required.

# DTO Rules

- DTOs live in `AIStudyHub.Business/DTOs/{ModuleName}`.
- Use DTOs for all API input and output.
- Do not expose entity classes from controllers.
- Keep request DTOs separate from response DTOs.
- Do not include sensitive fields in response DTOs.
- Never expose `PasswordHash`, secrets, tokens other than explicit auth response tokens, or provider credentials.
- Use AutoMapper profiles in `AIStudyHub.Business/Mappings`.

# Validation Rules

- Use FluentValidation.
- Validators live in `AIStudyHub.Business/Validators/{ModuleName}`.
- Validate required fields, max lengths, enum values, numeric ranges, and IDs.
- Validation should protect service logic from malformed requests.
- Do not duplicate validation rules in controllers unless required for HTTP-specific behavior.

# Security Rules

- Use JWT Bearer authentication.
- Store JWT settings under `Jwt` configuration.
- Do not commit real secrets, production JWT keys, certificates, passwords, or connection strings with credentials.
- Use user secrets, environment variables, or secure secret stores for sensitive values.
- Never return password hashes.
- Hash passwords before persistence when authentication logic is implemented.
- Apply role checks to admin endpoints.
- Enforce ownership checks in services for student-owned resources.
- Validate uploaded document metadata and file constraints before persistence.
- Treat AI chat content and generated learning content as user data.

# Logging Rules

- Use Serilog.
- Configure logging in `appsettings.json`.
- Use structured logging.
- Log unexpected exceptions in global middleware.
- Do not log passwords, JWTs, payment secrets, private document contents, or sensitive user data.
- Use request logging middleware for API request diagnostics.
- Prefer contextual logs around important business operations once business logic is implemented.

# Error Handling Rules

- Use global exception middleware in `AIStudyHub.API/Middleware`.
- Do not expose stack traces or internal exception details in production responses.
- Convert validation exceptions to `400 BadRequest`.
- Convert authentication failures to `401 Unauthorized`.
- Return consistent JSON error responses.
- Services should throw meaningful exceptions for business failures.
- Controllers should not contain broad try/catch blocks.

# Current Implementations & Future Development Notes

- Authentication is implemented using ASP.NET Core Identity with JWT and Refresh Tokens.
- AI features use a local LLM stack (Ollama + `nomic-embed-text`) for embeddings, with Qdrant for vector storage. Quizzes and Flashcards are auto-saved by the backend after generation.
- Document uploading uses `multipart/form-data` with explicit local chunking and vectorization (`DocumentUploadController`).
- Replace remaining skeleton `NotImplementedException` service methods with real business logic.
- Implement payment provider webhook handling.
- Add integration tests for controllers and service workflows.
- Add unit tests for validators, business rules, and repository behavior.
- Review `appsettings.json` before production deployment and move secrets out of source control.
