# AI Study Hub API Planning Document

## Document Purpose

This document defines the inferred business requirements, user journeys, workflows, and API contracts for the AI Study Hub system based on the current Entity Framework Core domain model. It is intended to be used immediately by frontend developers, backend developers, QA, and product stakeholders before service and controller implementation begins.

The current system source of truth is the entity model inside the data layer, especially entities such as `User`, `Document`, `Subject`, `TierMembership`, `TierUser`, `DocumentChunk`, `Flashcard`, `Quiz`, `Payment`, `Notification`, `Report`, `Vote`, `ChatSession`, and `ChatMessage`.

## Scope Assumptions

- The system is a multi-role AI-assisted learning document platform.
- Authentication is based on ASP.NET Core Identity with refresh-token support.
- Documents are user-owned and may be processed by AI services.
- Subscription tiers control storage and AI token usage.
- Payment is required for tier upgrades.
- AI-generated learning assets include document chunks, flashcards, quizzes, and AI chat responses.
- Missing entities are noted in the architectural review; some API requirements are inferred even where database support is incomplete.

# System Overview

## Core Modules

### 1. Authentication and Session Management
Responsible for registration, login, refresh token rotation, logout, password lifecycle, and account state validation.

Relevant entities:
- `User`
- `RefreshToken`

### 2. User and Profile Management
Responsible for profile retrieval, profile updates, account status, AI token balance, and storage usage tracking.

Relevant entities:
- `User`
- `TierUser`
- `TierMembership`

### 3. Membership and Subscription Management
Responsible for tier definition, tier assignment, upgrade history, quota enforcement, and subscription visibility.

Relevant entities:
- `TierMembership`
- `TierUser`
- `User`
- `Payment`

### 4. Payment Management
Responsible for payment initialization, third-party payment callback handling, payment status tracking, and upgrade activation.

Relevant entities:
- `Payment`
- `TierMembership`
- `User`

### 5. Subject Catalog Management
Responsible for academic subject organization and document categorization.

Relevant entities:
- `Subject`
- `Document`

### 6. Document Management
Responsible for document upload, metadata updates, retrieval, archival, publication, filtering, and ownership.

Relevant entities:
- `Document`
- `User`
- `Subject`

### 7. Document Processing and AI Retrieval
Responsible for converting documents into AI-ready chunks and storing embeddings or serialized chunk payloads.

Relevant entities:
- `DocumentChunk`
- `Document`

### 8. AI Chat and Learning Assistance
Responsible for chat sessions and AI-generated conversation context around learning content.

Relevant entities:
- `ChatSession`
- `ChatMessage`
- `User`

### 9. Flashcard Learning Module
Responsible for flashcard generation, listing, editing, ordering, and deletion.

Relevant entities:
- `Flashcard`
- `Document`

### 10. Quiz and Assessment Module
Responsible for quiz generation, question storage, answer options, submissions, scoring, and history.

Relevant entities:
- `Quiz`
- `Question`
- `Answer`
- `QuizSubmission`
- `Document`
- `User`

### 11. Community Interaction Module
Responsible for document voting and feedback scoring.

Relevant entities:
- `Vote`
- `Document`
- `User`

### 12. Reporting and Moderation Module
Responsible for abuse reporting, moderation workflows, and admin resolution status management.

Relevant entities:
- `Report`
- `Document`
- `User`

### 13. Notification Module
Responsible for system, document, quiz, and payment notifications.

Relevant entities:
- `Notification`
- `User`

### 14. Administrative Operations Module
Responsible for operational oversight, user management, content moderation, payment review, and membership management.

Relevant entities:
- `User`
- `Document`
- `Report`
- `Payment`
- `TierMembership`
- `Notification`

## User Roles

### Guest
Inferred role for unauthenticated visitors. Not stored in the `UserRole` enum, but required for public browsing, registration, login, and viewing published public content.

Typical permissions:
- Register account
- Log in
- Browse public documents
- View public subject listings

### Student
Primary end-user role inferred from `UserRole.Student`.

Typical permissions:
- Manage own profile
- Upload and manage own documents
- Generate chunks, flashcards, quizzes
- Chat with AI
- Vote and report documents
- Receive notifications
- Upgrade tier and view payment history

### Educator
Advanced content creator role inferred from `UserRole.Educator`.

Typical permissions:
- All student capabilities
- Higher content management privileges
- Possible subject-level curation
- Likely stronger document publishing permissions

### Admin
Operational governance role inferred from `UserRole.Admin`.

Typical permissions:
- Manage users
- Moderate reports
- Review documents
- Manage payments
- Manage membership tiers
- Access dashboard and analytics
- Override content and account states

## Business Features

### User Account Features
- User registration
- Login with refresh token support
- Logout and token revocation
- Password change and reset workflow
- Account activation / deactivation
- Role-based access control

### Learning Document Features
- Upload learning documents
- Assign documents to subjects
- Draft / publish / archive documents
- Search and browse documents
- View document details
- Manage personal document library

### AI Features
- Generate document chunks
- Store embeddings for retrieval workflows
- Run AI chat sessions
- Generate flashcards from documents
- Generate quizzes from documents
- Track AI token consumption

### Subscription Features
- View current membership tier
- Upgrade membership tier
- View tier history
- Enforce storage quota
- Enforce AI token limits

### Payment Features
- Create payment transactions
- Track payment status
- Complete or fail tier upgrade payments
- View payment history
- Support callback/webhook processing

### Social and Moderation Features
- Upvote or downvote documents
- Remove vote
- Report inappropriate or invalid documents
- Review and resolve reports

### Notification Features
- Notification feed
- Read/unread notification state
- Bulk mark-as-read
- Event-driven notifications for payment, document, quiz, and system actions

### Chat Features
- Create chat sessions
- Persist chat history
- Ask AI questions within a session
- Delete chat sessions

### Quiz Features
- Generate quiz from a document
- Store questions and answer options
- Submit quiz attempts
- View quiz results and history

### Flashcard Features
- Generate flashcards from a document
- View flashcards
- Edit flashcards
- Delete flashcards

### Sharing Features
The requested scope requires sharing APIs, but the current model has no document-sharing entity. This implies a planned feature not yet modeled at database level.

# Business Flow Analysis

# User Registration and Authentication

## Purpose
Allow users to create an account, authenticate securely, maintain login sessions, and protect access to platform features.

## Actor
- Guest
- Authenticated User
- Admin

## Preconditions
- User email is not already registered for registration.
- User account is active for login.
- Password satisfies policy rules.

## Main Flow
1. Guest submits registration form.
2. System validates email uniqueness and password policy.
3. System creates user with default role `Student` and active status.
4. User logs in with email and password.
5. System validates credentials and account status.
6. System generates access token and refresh token.
7. Refresh token is stored in `RefreshToken` table.
8. User uses access token to call protected APIs.
9. When access token expires, client submits refresh token.
10. System rotates refresh token and returns a new token pair.
11. On logout, refresh token is revoked.

## Alternative Flow
- Registration email already exists.
- Login credentials are invalid.
- User account is inactive or locked.
- Refresh token is expired, revoked, or replaced.
- Password reset requested.

## Validation Rules
- Email required and valid format.
- Password required and must meet minimum complexity.
- Full name required.
- Refresh token must be active.

## Business Rules
- New users default to `Student` unless created by admin.
- Inactive users cannot log in.
- Token rotation invalidates previous refresh tokens.
- One or multiple refresh tokens per device can be supported; current model supports multiple rows.

## Security Rules
- Password must never be returned in any response.
- Refresh tokens must be stored hashed.
- Access to protected endpoints requires JWT bearer token.
- Password reset tokens should be short-lived.

# Document Management

## Purpose
Allow users to upload, classify, maintain, and retrieve learning documents.

## Actor
- Student
- Educator
- Admin

## Preconditions
- User is authenticated.
- User account is active.
- Storage quota is not exceeded.
- Subject exists if provided.

## Main Flow
1. User uploads a document with metadata.
2. System validates file type and file size.
3. System checks user storage against current tier quota.
4. System stores file in file storage.
5. System creates `Document` record with `Draft` status.
6. User can update title, description, and subject.
7. User can publish or archive the document depending on policy.
8. User or public clients can retrieve document detail depending on visibility policy.

## Alternative Flow
- Upload rejected due to quota exceeded.
- Unsupported content type.
- Missing or invalid file reference.
- Subject does not exist.
- User attempts to update a document they do not own.

## Validation Rules
- Title required.
- File URL required after successful upload.
- Content type required.
- File size must be positive.
- SubjectId must exist if provided.

## Business Rules
- A document belongs to exactly one owner.
- A document may belong to one subject.
- Draft documents are only visible to owner and admins.
- Archived documents should not appear in public search.
- Publishing rules may differ by role.

## Security Rules
- Only owner or admin can update/delete.
- File access should be secured via signed URL or controlled download endpoint.
- Public visibility must not expose non-published content.

# Document Sharing

## Purpose
Allow a document owner to share learning documents with other users.

## Actor
- Student
- Educator
- Admin

## Preconditions
- User is authenticated.
- Document exists.
- User owns the document or is admin.
- Target user exists.

## Main Flow
1. Owner selects a document to share.
2. Owner specifies target users or share mode.
3. System creates sharing relationship.
4. Shared users can list shared documents and open allowed details.
5. Owner can revoke access later.

## Alternative Flow
- Target user not found.
- Document already shared with same user.
- Owner revokes share.
- Share switched to public link mode.

## Validation Rules
- DocumentId required.
- At least one target user or one share mode required.
- Cannot share with self redundantly.

## Business Rules
- Sharing is separate from ownership.
- Revoked users lose access immediately.
- Public share, private share, and organization share are future-compatible modes.

## Security Rules
- Only owner or admin can manage sharing.
- Shared users can read only, unless explicitly granted edit rights in future.

## Important Note
This feature is required by requested scope but not supported by any current entity. A missing entity such as `DocumentShare` is needed.

# AI Chat

## Purpose
Allow users to interact with AI for study assistance, likely grounded in uploaded documents or document chunks.

## Actor
- Student
- Educator

## Preconditions
- User is authenticated.
- User account is active.
- User has remaining AI tokens.
- Chat session exists for continued conversation, or a new session is created.

## Main Flow
1. User creates a chat session.
2. User submits a prompt or question.
3. System checks token balance and access rights.
4. System optionally retrieves relevant document chunks.
5. System calls LLM service.
6. System stores user message and assistant reply in `ChatMessage`.
7. System decrements AI token balance.
8. User retrieves chat history later.

## Alternative Flow
- No AI tokens remaining.
- LLM service timeout.
- No relevant chunks found; AI responds generally.
- Session deleted by user.

## Validation Rules
- Prompt required and non-empty.
- Chat session must belong to current user.
- Message length must be bounded.

## Business Rules
- Only owner can access a chat session.
- AI token balance decreases per request.
- Chat history is preserved until deleted.

## Security Rules
- Prompts must be sanitized for logging and audit.
- Users may only access their own sessions.
- AI responses should not expose private documents without authorization.

# Flashcard Generation

## Purpose
Convert document content into flashcards to support active recall learning.

## Actor
- Student
- Educator

## Preconditions
- User is authenticated.
- User owns the document or has valid access.
- Document exists and has parsable content.
- User has sufficient AI tokens.

## Main Flow
1. User requests flashcard generation for a document.
2. System loads document content or chunks.
3. System sends generation request to AI service.
4. System stores generated `Flashcard` records linked to the document.
5. User views, edits, reorders, or deletes flashcards.

## Alternative Flow
- No extractable content.
- Generation partially succeeds.
- Existing flashcards already exist and user chooses regenerate or append.

## Validation Rules
- DocumentId required.
- Flashcard front and back cannot be empty for manual updates.
- SortOrder must be non-negative.

## Business Rules
- Flashcards are document-scoped.
- Regeneration may overwrite existing flashcards only if explicitly requested.
- Token usage should be tracked.

## Security Rules
- Only owner/admin/authorized shared user can view.
- Only owner/admin can generate, edit, or delete.

# Quiz Generation and Submission

## Purpose
Generate quizzes from learning content and allow learners to take assessments.

## Actor
- Student
- Educator
- Admin

## Preconditions
- User is authenticated.
- Document exists and is accessible.
- User has sufficient AI tokens to generate a quiz.
- Quiz exists to submit answers.

## Main Flow
1. User requests quiz generation from a document.
2. System generates `Quiz`, `Question`, and `Answer` records.
3. User opens quiz detail.
4. User answers questions and submits.
5. System scores submission.
6. System stores `QuizSubmission`.
7. User views result and history.

## Alternative Flow
- Quiz already exists for document.
- User retakes quiz.
- AI generation fails.
- Short answer questions require manual scoring in a future version.

## Validation Rules
- Quiz title required.
- At least one question required.
- Choice questions must have valid answer options.
- Submission requires a valid quiz.

## Business Rules
- Passing score is stored on quiz.
- Scoring must align with `QuestionType`.
- Short answer auto-evaluation may be approximate and should be disclosed.
- Submission history is user-specific.

## Security Rules
- Users can only submit for themselves.
- Hidden correct answers should not be returned during active quiz attempt.
- Admin and owner permissions may differ for quiz editing.

# Voting

## Purpose
Allow users to express quality feedback on documents.

## Actor
- Student
- Educator
- Admin

## Preconditions
- User is authenticated.
- Document exists and is visible.

## Main Flow
1. User chooses upvote or downvote for a document.
2. System checks if user already voted.
3. If no vote exists, system creates vote.
4. If vote exists with same type, system may keep unchanged.
5. If vote exists with opposite type, system updates type.
6. User may remove vote later.

## Alternative Flow
- Duplicate vote request.
- User changes vote type.
- Document not accessible.

## Validation Rules
- DocumentId required.
- Vote type must be `Upvote` or `Downvote`.

## Business Rules
- One vote per user per document.
- Vote is mutable.
- Vote summary should be derived efficiently.

## Security Rules
- Only authenticated users can vote.
- Users cannot spoof another user's identity.

# Reporting and Moderation

## Purpose
Allow users to report problematic content and allow admins to resolve moderation cases.

## Actor
- Student
- Educator
- Admin

## Preconditions
- Document exists.
- Reporting user is authenticated.

## Main Flow
1. User opens report form on a document.
2. User provides reason and optional details.
3. System creates `Report` with `Pending` status.
4. Admin retrieves report list.
5. Admin reviews report and updates status to `Reviewed`, `Resolved`, or `Rejected`.
6. System may notify reporter and/or document owner.

## Alternative Flow
- Duplicate report by same user for same document.
- Admin rejects invalid report.
- Admin resolves by archiving document or warning owner.

## Validation Rules
- Reason required.
- Details max length enforced.
- Valid report status transitions only.

## Business Rules
- Report lifecycle: Pending -> Reviewed -> Resolved/Rejected.
- Multiple reports may exist for one document.
- Admin actions may drive document moderation status in future.

## Security Rules
- Only admins can view all reports and resolve them.
- Reporter identity may be hidden from document owner.

# Payment and Subscription

## Purpose
Allow users to upgrade membership tiers through payment and receive quota changes.

## Actor
- Student
- Educator
- Admin
- Payment Gateway

## Preconditions
- User is authenticated for initiating payment.
- Target tier exists.
- Tier differs from current or represents a valid renewal path.

## Main Flow
1. User views available tiers.
2. User selects a target tier.
3. System creates `Payment` record with `Pending` status.
4. System returns payment URL or provider payload.
5. External gateway processes payment.
6. Gateway callback updates payment to `Completed` or `Failed`.
7. On success, system creates `TierUser` history entry and updates active quota state.
8. User can view payment history and current tier.

## Alternative Flow
- Payment fails or expires.
- Callback is duplicated.
- Payment is refunded.
- User retries payment.

## Validation Rules
- TierId required for upgrade payment.
- Amount must be positive.
- Provider required.
- Callback signature must validate.

## Business Rules
- Only completed payments activate upgrades.
- Tier history should remain auditable.
- Quotas should be recalculated after upgrade.
- `TierUser` implies historical assignment, but additional start/end dates are currently missing.

## Security Rules
- Payment callback must verify signature/source.
- Clients must never control final payment status.
- Admin-only payment overrides must be audited.

# Notification

## Purpose
Provide event-driven user communication for important actions.

## Actor
- Student
- Educator
- Admin
- System

## Preconditions
- User exists.
- A triggering business event occurred.

## Main Flow
1. A system event occurs, such as payment update, quiz availability, or moderation message.
2. System creates a `Notification` with type and read state.
3. User fetches notification list.
4. User marks one or all notifications as read.

## Alternative Flow
- Notification already read.
- Notification list filtered by type.

## Validation Rules
- Notification must have title and message.
- Type must match defined enum values.

## Business Rules
- Notifications are user-scoped.
- Unread count should be available efficiently.
- Notifications may later support pagination and soft deletion.

## Security Rules
- Users can only read their own notifications.
- Admin notifications may contain sensitive operations.

# Admin Moderation and Operations

## Purpose
Allow platform operators to manage users, documents, reports, payments, and tiers.

## Actor
- Admin

## Preconditions
- Authenticated admin account.
- Admin role assigned.

## Main Flow
1. Admin opens dashboard.
2. System returns operational summaries.
3. Admin searches users, documents, reports, and payments.
4. Admin updates statuses as needed.
5. Admin manages membership tiers.

## Alternative Flow
- Admin suspends a user.
- Admin archives a document.
- Admin rejects fraudulent payment callback.

## Validation Rules
- Role check required on every admin endpoint.
- Entity existence required before update.

## Business Rules
- Admin operations override end-user ownership constraints.
- Sensitive actions should create audit logs.

## Security Rules
- Admin endpoints must require elevated authorization.
- Sensitive actions should use strong auditing and least privilege.

# API Planning

## API Conventions

- Base route prefix: `/api`
- Authentication: JWT Bearer Token
- Standard response envelope recommended:

```json
{
  "success": true,
  "message": "Operation completed successfully.",
  "data": {}
}
```

- Standard error envelope recommended:

```json
{
  "success": false,
  "message": "Validation failed.",
  "errors": [
    {
      "code": "VALIDATION_ERROR",
      "field": "email",
      "message": "Email is required."
    }
  ]
}
```

- Common status codes:
  - `200 OK`
  - `201 Created`
  - `204 No Content`
  - `400 Bad Request`
  - `401 Unauthorized`
  - `403 Forbidden`
  - `404 Not Found`
  - `409 Conflict`
  - `422 Unprocessable Entity`
  - `429 Too Many Requests`
  - `500 Internal Server Error`

## AUTH MODULE

# Register

### API Name
Register User

### Endpoint
`POST /api/auth/register`

### Method
`POST`

### Authorization
Guest

### Description
Create a new user account with default student role.

### Request DTO

```json
{
  "fullName": "Nguyen Van A",
  "email": "student@example.com",
  "password": "StrongPass@123",
  "confirmPassword": "StrongPass@123",
  "dateOfBirth": "2003-09-15"
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Registration successful.",
  "data": {
    "userId": "c3d8bd3d-fdc7-4d85-b4bf-4fe4b074859f",
    "role": "Student"
  }
}
```

### Validation Rules
- `fullName` required, max 255 chars.
- `email` required, valid email format.
- `password` required, min length, upper/lower/number/special char.
- `confirmPassword` must match `password`.

### Business Rules
- Email must be unique.
- New account default role is `Student`.
- New account default status is active unless verification is introduced later.

### Error Responses
- `400` invalid payload
- `409` email already exists
- `422` password policy violation

# Login

### API Name
Login

### Endpoint
`POST /api/auth/login`

### Method
`POST`

### Authorization
Guest

### Description
Authenticate user and issue access and refresh tokens.

### Request DTO

```json
{
  "email": "student@example.com",
  "password": "StrongPass@123"
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "accessToken": "jwt-token",
    "refreshToken": "plain-refresh-token",
    "expiresInSeconds": 3600,
    "user": {
      "id": "c3d8bd3d-fdc7-4d85-b4bf-4fe4b074859f",
      "fullName": "Nguyen Van A",
      "email": "student@example.com",
      "role": "Student",
      "isActive": true
    }
  }
}
```

### Validation Rules
- Email required.
- Password required.

### Business Rules
- Only active users can log in.
- Refresh token must be persisted hashed.

### Error Responses
- `400` invalid payload
- `401` invalid credentials
- `403` inactive account

# Refresh Token

### API Name
Refresh Access Token

### Endpoint
`POST /api/auth/refresh-token`

### Method
`POST`

### Authorization
Guest with refresh token

### Description
Rotate refresh token and issue a new access token.

### Request DTO

```json
{
  "refreshToken": "plain-refresh-token"
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "accessToken": "new-jwt-token",
    "refreshToken": "new-plain-refresh-token",
    "expiresInSeconds": 3600
  }
}
```

### Validation Rules
- `refreshToken` required.

### Business Rules
- Expired or revoked tokens are rejected.
- Rotation revokes old token and stores replacement.

### Error Responses
- `401` invalid token
- `409` token already rotated

# Logout

### API Name
Logout

### Endpoint
`POST /api/auth/logout`

### Method
`POST`

### Authorization
Authenticated User

### Description
Revoke current refresh token and terminate session.

### Request DTO

```json
{
  "refreshToken": "plain-refresh-token"
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Logged out successfully."
}
```

### Validation Rules
- `refreshToken` required.

### Business Rules
- Token is revoked even if access token remains temporarily valid.

### Error Responses
- `400` invalid request
- `401` unauthorized

# Change Password

### API Name
Change Password

### Endpoint
`POST /api/auth/change-password`

### Method
`POST`

### Authorization
Authenticated User

### Description
Change current password using old password verification.

### Request DTO

```json
{
  "currentPassword": "OldPass@123",
  "newPassword": "NewPass@456",
  "confirmNewPassword": "NewPass@456"
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Password changed successfully."
}
```

### Validation Rules
- All fields required.
- New password must match confirmation.
- New password must differ from current.

### Business Rules
- Existing refresh tokens may be revoked after password change.

### Error Responses
- `400` invalid request
- `401` current password incorrect
- `422` password policy violation

# Forgot Password

### API Name
Forgot Password

### Endpoint
`POST /api/auth/forgot-password`

### Method
`POST`

### Authorization
Guest

### Description
Initiate password reset flow.

### Request DTO

```json
{
  "email": "student@example.com"
}
```

### Response DTO

```json
{
  "success": true,
  "message": "If the email exists, a reset link has been sent."
}
```

### Validation Rules
- Email required.

### Business Rules
- Response should not reveal whether account exists.
- Requires reset token/email infrastructure not yet modeled.

### Error Responses
- `400` invalid payload
- `429` too many requests

## USER MODULE

# Get My Profile

### API Name
Get Profile

### Endpoint
`GET /api/users/me`

### Method
`GET`

### Authorization
Student, Educator, Admin

### Description
Return current user profile and account quota data.

### Response DTO

```json
{
  "success": true,
  "data": {
    "id": "c3d8bd3d-fdc7-4d85-b4bf-4fe4b074859f",
    "fullName": "Nguyen Van A",
    "email": "student@example.com",
    "dateOfBirth": "2003-09-15",
    "role": "Student",
    "status": "Active",
    "isActive": true,
    "currentStorageCapacity": 250,
    "currentAiToken": 1200,
    "tierId": "4f9e8fd0-76e3-4dc3-9aeb-5e31dbca74a4"
  }
}
```

### Validation Rules
- Valid access token required.

### Business Rules
- Returns only caller profile unless admin-targeted endpoint is used.

### Error Responses
- `401` unauthorized

# Update My Profile

### API Name
Update Profile

### Endpoint
`PUT /api/users/me`

### Method
`PUT`

### Authorization
Student, Educator, Admin

### Description
Update current user profile fields.

### Request DTO

```json
{
  "fullName": "Nguyen Van B",
  "dateOfBirth": "2002-05-20"
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Profile updated successfully."
}
```

### Validation Rules
- `fullName` required.
- `dateOfBirth` cannot be future date.

### Business Rules
- Email change should be separate if verification is required.

### Error Responses
- `400` invalid payload
- `401` unauthorized

# Get Storage Usage

### API Name
Get Storage Usage

### Endpoint
`GET /api/users/me/storage-usage`

### Method
`GET`

### Authorization
Student, Educator, Admin

### Description
Return storage usage and quota summary for current user.

### Response DTO

```json
{
  "success": true,
  "data": {
    "usedBytes": 262144000,
    "usedMb": 250,
    "quotaMb": 1024,
    "remainingMb": 774,
    "usagePercent": 24.41
  }
}
```

### Validation Rules
- Auth required.

### Business Rules
- `usedBytes` should be aggregated from user documents.
- `quotaMb` derived from current membership tier.

### Error Responses
- `401` unauthorized

## SUBSCRIPTION MODULE

# View Current Tier

### API Name
Get Current Tier

### Endpoint
`GET /api/subscriptions/current-tier`

### Method
`GET`

### Authorization
Student, Educator, Admin

### Description
Return active membership tier and quotas.

### Response DTO

```json
{
  "success": true,
  "data": {
    "tierId": "4f9e8fd0-76e3-4dc3-9aeb-5e31dbca74a4",
    "tierName": "Premium",
    "storageLimitMb": 1024,
    "aiTokens": 5000,
    "currentStorageCapacity": 250,
    "currentAiToken": 1200
  }
}
```

### Validation Rules
- Auth required.

### Business Rules
- Active tier inference is currently ambiguous because `TierUser` has no validity dates.
- Temporary rule: latest `TierUser.CreatedAt` may represent active assignment until richer model exists.

### Error Responses
- `401` unauthorized
- `404` no tier assigned

# Upgrade Tier

### API Name
Upgrade Tier

### Endpoint
`POST /api/subscriptions/upgrade`

### Method
`POST`

### Authorization
Student, Educator

### Description
Start upgrade workflow by creating a payment for target tier.

### Request DTO

```json
{
  "tierId": "4f9e8fd0-76e3-4dc3-9aeb-5e31dbca74a4",
  "provider": "VNPay"
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "paymentId": "8f0f6c8f-d8bc-4bb0-90f5-543119fef255",
    "paymentUrl": "https://sandbox-payment.example/redirect",
    "status": "Pending"
  }
}
```

### Validation Rules
- `tierId` required.
- `provider` required.

### Business Rules
- Cannot downgrade through this endpoint unless explicitly supported.
- Payment creation should determine price from tier pricing source, which is currently missing from entity model.

### Error Responses
- `400` invalid payload
- `404` tier not found
- `409` already on target tier

# Tier History

### API Name
Get Tier History

### Endpoint
`GET /api/subscriptions/history`

### Method
`GET`

### Authorization
Student, Educator, Admin

### Description
Return tier assignment history for current user.

### Response DTO

```json
{
  "success": true,
  "data": [
    {
      "tierUserId": "6aef1d1e-77ef-4e69-96bc-0628f3f41e8f",
      "tierId": "4f9e8fd0-76e3-4dc3-9aeb-5e31dbca74a4",
      "tierName": "Premium",
      "assignedAt": "2026-06-01T09:20:00Z"
    }
  ]
}
```

### Validation Rules
- Auth required.

### Business Rules
- History precision is limited because `TierUser` lacks effective dates and end dates.

### Error Responses
- `401` unauthorized

## PAYMENT MODULE

# Create Payment

### API Name
Create Payment

### Endpoint
`POST /api/payments`

### Method
`POST`

### Authorization
Student, Educator

### Description
Create a payment transaction for a tier purchase or renewal.

### Request DTO

```json
{
  "tierId": "4f9e8fd0-76e3-4dc3-9aeb-5e31dbca74a4",
  "provider": "VNPay"
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "paymentId": "8f0f6c8f-d8bc-4bb0-90f5-543119fef255",
    "amount": 199000,
    "currency": "VND",
    "provider": "VNPay",
    "paymentUrl": "https://sandbox-payment.example/redirect",
    "status": "Pending"
  }
}
```

### Validation Rules
- `tierId` required.
- Provider must be supported.

### Business Rules
- Amount should be system-generated, not client-generated.
- `Payment.Status` starts as `Pending`.

### Error Responses
- `400` invalid payload
- `404` tier not found
- `409` existing pending payment conflict

# Payment Callback

### API Name
Handle Payment Callback

### Endpoint
`POST /api/payments/callback`

### Method
`POST`

### Authorization
Payment Gateway / Internal Signature

### Description
Receive payment result from provider and update payment record.

### Request DTO

```json
{
  "provider": "VNPay",
  "providerTransactionId": "VNP202606090001",
  "paymentId": "8f0f6c8f-d8bc-4bb0-90f5-543119fef255",
  "status": "Completed",
  "signature": "gateway-signature"
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Payment callback processed."
}
```

### Validation Rules
- Signature required.
- Provider transaction id required.
- PaymentId required.

### Business Rules
- Idempotent processing required.
- On `Completed`, create tier history row and update user quota.
- On `Failed`, payment remains failed without quota update.

### Error Responses
- `400` invalid payload
- `401` invalid signature
- `404` payment not found
- `409` duplicate finalized callback

# Payment History

### API Name
Get Payment History

### Endpoint
`GET /api/payments/history`

### Method
`GET`

### Authorization
Student, Educator, Admin

### Description
Return current user's payment history.

### Response DTO

```json
{
  "success": true,
  "data": [
    {
      "paymentId": "8f0f6c8f-d8bc-4bb0-90f5-543119fef255",
      "tierId": "4f9e8fd0-76e3-4dc3-9aeb-5e31dbca74a4",
      "tierName": "Premium",
      "amount": 199000,
      "currency": "VND",
      "provider": "VNPay",
      "providerTransactionId": "VNP202606090001",
      "status": "Completed",
      "createdAt": "2026-06-09T10:00:00Z"
    }
  ]
}
```

### Validation Rules
- Auth required.

### Business Rules
- Non-admin users only see own payments.

### Error Responses
- `401` unauthorized

## SUBJECT MODULE

# Create Subject

### API Name
Create Subject

### Endpoint
`POST /api/subjects`

### Method
`POST`

### Authorization
Admin

### Description
Create a new academic subject.

### Request DTO

```json
{
  "subjectCode": "MATH101",
  "subjectName": "Mathematics",
  "description": "Core mathematics learning resources."
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "subjectId": "8774fc52-dad5-4168-8dca-22b210ce2c91"
  }
}
```

### Validation Rules
- `subjectCode` required, max 20 chars.
- `subjectName` required, max 255 chars.

### Business Rules
- `subjectCode` must be unique.

### Error Responses
- `400` invalid payload
- `409` duplicate subject code

# Update Subject

### API Name
Update Subject

### Endpoint
`PUT /api/subjects/{subjectId}`

### Method
`PUT`

### Authorization
Admin

### Description
Update an existing subject.

### Request DTO

```json
{
  "subjectCode": "MATH101",
  "subjectName": "Advanced Mathematics",
  "description": "Updated description"
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Subject updated successfully."
}
```

### Validation Rules
- Same as create.

### Business Rules
- Subject code must remain unique.

### Error Responses
- `400`, `404`, `409`

# Delete Subject

### API Name
Delete Subject

### Endpoint
`DELETE /api/subjects/{subjectId}`

### Method
`DELETE`

### Authorization
Admin

### Description
Delete a subject if allowed by business constraints.

### Response DTO

```json
{
  "success": true,
  "message": "Subject deleted successfully."
}
```

### Validation Rules
- `subjectId` valid GUID.

### Business Rules
- Hard delete should be blocked if documents are linked, or require reassignment.

### Error Responses
- `404` subject not found
- `409` subject in use

# Get Subject List

### API Name
List Subjects

### Endpoint
`GET /api/subjects`

### Method
`GET`

### Authorization
Guest / Authenticated User

### Description
Return subject catalog.

### Response DTO

```json
{
  "success": true,
  "data": [
    {
      "subjectId": "8774fc52-dad5-4168-8dca-22b210ce2c91",
      "subjectCode": "MATH101",
      "subjectName": "Mathematics",
      "description": "Core mathematics learning resources."
    }
  ]
}
```

### Validation Rules
- Optional search query allowed.

### Business Rules
- Public listing is acceptable because subject metadata is non-sensitive.

### Error Responses
- `400` invalid filter

# Get Subject Detail

### API Name
Get Subject Detail

### Endpoint
`GET /api/subjects/{subjectId}`

### Method
`GET`

### Authorization
Guest / Authenticated User

### Description
Return single subject detail.

### Response DTO

```json
{
  "success": true,
  "data": {
    "subjectId": "8774fc52-dad5-4168-8dca-22b210ce2c91",
    "subjectCode": "MATH101",
    "subjectName": "Mathematics",
    "description": "Core mathematics learning resources."
  }
}
```

### Validation Rules
- `subjectId` required.

### Business Rules
- Can optionally include document counts.

### Error Responses
- `404` subject not found

## DOCUMENT MODULE

# Upload Document

### API Name
Create Document

### Endpoint
`POST /api/documents`

### Method
`POST`

### Authorization
Student, Educator, Admin

### Description
Upload a learning document and create its metadata record.

### Request DTO

```json
{
  "subjectId": "8774fc52-dad5-4168-8dca-22b210ce2c91",
  "title": "AI Notes",
  "description": "Lecture summary for week 1",
  "contentType": "application/pdf",
  "fileSizeBytes": 1048576,
  "file": "multipart-file"
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "documentId": "6027360e-d010-4a5c-9fe9-d4f9efee60fd",
    "status": "Draft",
    "fileUrl": "https://storage.example/documents/6027360e.pdf"
  }
}
```

### Validation Rules
- `title` required.
- `file` required.
- `contentType` required.
- `fileSizeBytes` positive.
- `subjectId` optional but must exist if provided.

### Business Rules
- User storage quota must not be exceeded.
- New document starts in `Draft`.
- File URL should be server-generated after storage upload.

### Error Responses
- `400` invalid request
- `401` unauthorized
- `403` inactive account
- `409` quota exceeded
- `415` unsupported file type

# Update Document

### API Name
Update Document

### Endpoint
`PUT /api/documents/{documentId}`

### Method
`PUT`

### Authorization
Student, Educator, Admin

### Description
Update document metadata and optionally status.

### Request DTO

```json
{
  "subjectId": "8774fc52-dad5-4168-8dca-22b210ce2c91",
  "title": "AI Notes - Updated",
  "description": "Updated lecture summary",
  "status": "Published"
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Document updated successfully."
}
```

### Validation Rules
- `title` required.
- `status` must be `Draft`, `Published`, or `Archived`.

### Business Rules
- Only owner or admin can update.
- Publish action may require successful processing or policy checks.

### Error Responses
- `400`, `401`, `403`, `404`

# Delete Document

### API Name
Delete Document

### Endpoint
`DELETE /api/documents/{documentId}`

### Method
`DELETE`

### Authorization
Student, Educator, Admin

### Description
Delete owned document and related generated assets.

### Response DTO

```json
{
  "success": true,
  "message": "Document deleted successfully."
}
```

### Validation Rules
- `documentId` valid GUID.

### Business Rules
- Delete should cascade to chunks, flashcards, quizzes, questions, answers, votes, and reports according to configured relationships or explicit orchestration.

### Error Responses
- `401`, `403`, `404`

# Get Document

### API Name
Get Document Detail

### Endpoint
`GET /api/documents/{documentId}`

### Method
`GET`

### Authorization
Owner, Shared User, Admin, or Guest for public documents

### Description
Return full document metadata and summary information.

### Response DTO

```json
{
  "success": true,
  "data": {
    "documentId": "6027360e-d010-4a5c-9fe9-d4f9efee60fd",
    "userId": "c3d8bd3d-fdc7-4d85-b4bf-4fe4b074859f",
    "subjectId": "8774fc52-dad5-4168-8dca-22b210ce2c91",
    "subjectName": "Mathematics",
    "title": "AI Notes",
    "description": "Lecture summary for week 1",
    "contentType": "application/pdf",
    "fileSizeBytes": 1048576,
    "status": "Published",
    "createdAt": "2026-06-09T08:00:00Z",
    "voteSummary": {
      "upvotes": 12,
      "downvotes": 1,
      "score": 11
    }
  }
}
```

### Validation Rules
- `documentId` required.

### Business Rules
- Visibility depends on ownership, sharing, and publication status.

### Error Responses
- `403` forbidden
- `404` not found

# Search Document

### API Name
Search Documents

### Endpoint
`GET /api/documents/search`

### Method
`GET`

### Authorization
Guest / Authenticated User

### Description
Search documents by keyword, subject, owner, and status filters.

### Request DTO
Query params example:

```json
{
  "keyword": "AI",
  "subjectId": "8774fc52-dad5-4168-8dca-22b210ce2c91",
  "status": "Published",
  "page": 1,
  "pageSize": 20
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "documentId": "6027360e-d010-4a5c-9fe9-d4f9efee60fd",
        "title": "AI Notes",
        "subjectName": "Mathematics",
        "status": "Published",
        "createdAt": "2026-06-09T08:00:00Z"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  }
}
```

### Validation Rules
- `page` and `pageSize` positive.
- `pageSize` bounded to protect performance.

### Business Rules
- Guests should only search published/public documents.
- Authenticated users can search their drafts in dedicated views.

### Error Responses
- `400` invalid query params

# Public Documents

### API Name
List Public Documents

### Endpoint
`GET /api/documents/public`

### Method
`GET`

### Authorization
Guest / Authenticated User

### Description
Return published documents intended for public browsing.

### Response DTO
Same structure as search result.

### Validation Rules
- Standard pagination.

### Business Rules
- Only `Published` documents returned.

### Error Responses
- `400` invalid query params

# My Documents

### API Name
List My Documents

### Endpoint
`GET /api/documents/my`

### Method
`GET`

### Authorization
Student, Educator, Admin

### Description
Return documents owned by current user.

### Response DTO
Same structure as search result with optional owner-only metadata.

### Validation Rules
- Auth required.

### Business Rules
- Includes all statuses for owner.

### Error Responses
- `401` unauthorized

# Shared Documents

### API Name
List Shared Documents

### Endpoint
`GET /api/documents/shared`

### Method
`GET`

### Authorization
Student, Educator, Admin

### Description
Return documents shared with current user.

### Response DTO
Search-style paged list.

### Validation Rules
- Auth required.

### Business Rules
- Requires missing share entity support.

### Error Responses
- `501` not implementable until sharing model exists

# Document Detail

### API Name
Get Document Detail View

### Endpoint
`GET /api/documents/{documentId}/detail`

### Method
`GET`

### Authorization
Owner, Shared User, Admin, or Guest for public documents

### Description
Return enriched detail view including generated asset counts and access flags.

### Response DTO

```json
{
  "success": true,
  "data": {
    "documentId": "6027360e-d010-4a5c-9fe9-d4f9efee60fd",
    "title": "AI Notes",
    "description": "Lecture summary for week 1",
    "status": "Published",
    "subject": {
      "subjectId": "8774fc52-dad5-4168-8dca-22b210ce2c91",
      "subjectName": "Mathematics"
    },
    "owner": {
      "userId": "c3d8bd3d-fdc7-4d85-b4bf-4fe4b074859f",
      "fullName": "Nguyen Van A"
    },
    "statistics": {
      "flashcardCount": 20,
      "quizCount": 2,
      "chunkCount": 40,
      "upvotes": 12,
      "downvotes": 1,
      "reportCount": 0
    },
    "permissions": {
      "canEdit": true,
      "canDelete": true,
      "canGenerateFlashcards": true,
      "canGenerateQuiz": true,
      "canChat": true
    }
  }
}
```

### Validation Rules
- `documentId` required.

### Business Rules
- Enriched projection should be optimized through aggregation.

### Error Responses
- `403`, `404`

## DOCUMENT SHARING MODULE

# Share Document

### API Name
Share Document

### Endpoint
`POST /api/documents/{documentId}/shares`

### Method
`POST`

### Authorization
Student, Educator, Admin

### Description
Share a document with target users.

### Request DTO

```json
{
  "userIds": [
    "11111111-1111-1111-1111-111111111111",
    "22222222-2222-2222-2222-222222222222"
  ]
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Document shared successfully."
}
```

### Validation Rules
- `userIds` required and non-empty.

### Business Rules
- Requires future `DocumentShare` entity.

### Error Responses
- `404` document/user not found
- `409` already shared
- `501` missing data model support

# Revoke Share

### API Name
Revoke Share

### Endpoint
`DELETE /api/documents/{documentId}/shares/{userId}`

### Method
`DELETE`

### Authorization
Student, Educator, Admin

### Description
Revoke a shared user's access.

### Response DTO

```json
{
  "success": true,
  "message": "Share revoked successfully."
}
```

### Validation Rules
- `documentId` and `userId` required.

### Business Rules
- Owner or admin only.

### Error Responses
- `404`, `501`

# Get Shared Users

### API Name
Get Shared Users

### Endpoint
`GET /api/documents/{documentId}/shares`

### Method
`GET`

### Authorization
Student, Educator, Admin

### Description
List users who currently have access through sharing.

### Response DTO

```json
{
  "success": true,
  "data": [
    {
      "userId": "11111111-1111-1111-1111-111111111111",
      "fullName": "Tran Thi B",
      "email": "user2@example.com"
    }
  ]
}
```

### Validation Rules
- `documentId` required.

### Business Rules
- Requires future `DocumentShare` entity.

### Error Responses
- `404`, `501`

## DOCUMENT CHUNK MODULE

# Generate Chunks

### API Name
Generate Document Chunks

### Endpoint
`POST /api/documents/{documentId}/chunks/generate`

### Method
`POST`

### Authorization
Student, Educator, Admin

### Description
Generate or regenerate AI chunks and embeddings for a document.

### Request DTO

```json
{
  "overwriteExisting": true,
  "chunkStrategy": "paragraph",
  "embeddingProvider": "OpenAI"
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "documentId": "6027360e-d010-4a5c-9fe9-d4f9efee60fd",
    "generatedChunkCount": 40
  }
}
```

### Validation Rules
- Valid document id required.
- Optional strategy/provider values must be supported.

### Business Rules
- Only owner or admin can generate.
- Existing chunks may be replaced when `overwriteExisting` is true.
- AI tokens may be consumed depending on implementation.

### Error Responses
- `400`, `403`, `404`, `409`

# View Chunks

### API Name
List Document Chunks

### Endpoint
`GET /api/documents/{documentId}/chunks`

### Method
`GET`

### Authorization
Owner, Admin

### Description
Return stored chunks for debugging, review, or AI inspection.

### Response DTO

```json
{
  "success": true,
  "data": [
    {
      "chunkId": "bd1d2016-1a6a-4ebc-a1bf-18dd4106d2a5",
      "chunkJson": "{\"text\":\"Introduction to AI\"}",
      "embeddingJson": "[0.12,0.52,0.77]"
    }
  ]
}
```

### Validation Rules
- `documentId` required.

### Business Rules
- Shared or public exposure of embeddings is discouraged.

### Error Responses
- `403`, `404`

## AI CHAT MODULE

# Create Chat Session

### API Name
Create Chat Session

### Endpoint
`POST /api/chat/sessions`

### Method
`POST`

### Authorization
Student, Educator, Admin

### Description
Create a new AI chat session.

### Request DTO

```json
{
  "title": "Ask about AI Notes"
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "chatSessionId": "0d43c520-2d2e-4ef4-8cfc-db9cf80e756e",
    "title": "Ask about AI Notes"
  }
}
```

### Validation Rules
- `title` required, max 200 chars.

### Business Rules
- Session belongs to current user.

### Error Responses
- `400`, `401`

# Ask AI

### API Name
Ask AI

### Endpoint
`POST /api/chat/sessions/{chatSessionId}/messages`

### Method
`POST`

### Authorization
Student, Educator, Admin

### Description
Send user prompt and receive AI reply while persisting both messages.

### Request DTO

```json
{
  "documentId": "6027360e-d010-4a5c-9fe9-d4f9efee60fd",
  "prompt": "Summarize the key concepts in this document."
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "userMessageId": "8b406c27-c86e-4ad2-81ae-7d7ca146d178",
    "assistantMessageId": "b902372a-7082-46ec-bb6b-6f6297745b6d",
    "assistantReply": "This document explains the foundations of AI...",
    "remainingAiTokens": 1180
  }
}
```

### Validation Rules
- `prompt` required.
- `documentId` optional but must be accessible if provided.

### Business Rules
- Decrement AI tokens after successful generation.
- `Role` in `ChatMessage` should be `user` or `assistant`.

### Error Responses
- `400`, `403`, `404`, `409`, `429`

# Get Chat History

### API Name
Get Chat History

### Endpoint
`GET /api/chat/sessions/{chatSessionId}`

### Method
`GET`

### Authorization
Student, Educator, Admin

### Description
Return chat session and all messages.

### Response DTO

```json
{
  "success": true,
  "data": {
    "chatSessionId": "0d43c520-2d2e-4ef4-8cfc-db9cf80e756e",
    "title": "Ask about AI Notes",
    "messages": [
      {
        "messageId": "8b406c27-c86e-4ad2-81ae-7d7ca146d178",
        "role": "user",
        "content": "Summarize the key concepts in this document.",
        "createdAt": "2026-06-09T11:30:00Z"
      },
      {
        "messageId": "b902372a-7082-46ec-bb6b-6f6297745b6d",
        "role": "assistant",
        "content": "This document explains the foundations of AI...",
        "createdAt": "2026-06-09T11:30:03Z"
      }
    ]
  }
}
```

### Validation Rules
- `chatSessionId` required.

### Business Rules
- Users can only access own sessions.

### Error Responses
- `403`, `404`

# Delete Chat Session

### API Name
Delete Chat Session

### Endpoint
`DELETE /api/chat/sessions/{chatSessionId}`

### Method
`DELETE`

### Authorization
Student, Educator, Admin

### Description
Delete a chat session and all messages.

### Response DTO

```json
{
  "success": true,
  "message": "Chat session deleted successfully."
}
```

### Validation Rules
- `chatSessionId` required.

### Business Rules
- Delete should cascade to chat messages.

### Error Responses
- `403`, `404`

## FLASHCARD MODULE

# Generate Flashcards

### API Name
Generate Flashcards

### Endpoint
`POST /api/documents/{documentId}/flashcards/generate`

### Method
`POST`

### Authorization
Student, Educator, Admin

### Description
Generate flashcards from document content.

### Request DTO

```json
{
  "count": 20,
  "overwriteExisting": false
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "documentId": "6027360e-d010-4a5c-9fe9-d4f9efee60fd",
    "generatedCount": 20
  }
}
```

### Validation Rules
- `count` positive and bounded.

### Business Rules
- Requires AI tokens.
- Only owner or admin can generate.

### Error Responses
- `400`, `403`, `404`, `409`, `429`

# List Flashcards

### API Name
List Flashcards

### Endpoint
`GET /api/documents/{documentId}/flashcards`

### Method
`GET`

### Authorization
Owner, Shared User, Admin

### Description
Return flashcards for a document.

### Response DTO

```json
{
  "success": true,
  "data": [
    {
      "flashcardId": "912fbc40-ecef-4c15-91fc-c17b9648886c",
      "front": "What is AI?",
      "back": "AI is the simulation of human intelligence by machines.",
      "sortOrder": 1
    }
  ]
}
```

### Validation Rules
- `documentId` required.

### Business Rules
- Ordering by `sortOrder` ascending.

### Error Responses
- `403`, `404`

# Update Flashcard

### API Name
Update Flashcard

### Endpoint
`PUT /api/flashcards/{flashcardId}`

### Method
`PUT`

### Authorization
Owner, Admin

### Description
Update flashcard content or order.

### Request DTO

```json
{
  "front": "Define AI",
  "back": "Artificial Intelligence is...",
  "sortOrder": 1
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Flashcard updated successfully."
}
```

### Validation Rules
- `front` and `back` required.

### Business Rules
- Only owner/admin can modify.

### Error Responses
- `400`, `403`, `404`

# Delete Flashcard

### API Name
Delete Flashcard

### Endpoint
`DELETE /api/flashcards/{flashcardId}`

### Method
`DELETE`

### Authorization
Owner, Admin

### Description
Delete a flashcard.

### Response DTO

```json
{
  "success": true,
  "message": "Flashcard deleted successfully."
}
```

### Validation Rules
- `flashcardId` required.

### Business Rules
- Remaining cards may require reindexing sort order.

### Error Responses
- `403`, `404`

## QUIZ MODULE

# Generate Quiz

### API Name
Generate Quiz

### Endpoint
`POST /api/documents/{documentId}/quizzes/generate`

### Method
`POST`

### Authorization
Student, Educator, Admin

### Description
Generate a quiz from a document.

### Request DTO

```json
{
  "title": "AI Basics Quiz",
  "description": "Auto-generated quiz from notes",
  "questionCount": 10,
  "timeLimitMinutes": 15,
  "passingScore": 70
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "quizId": "12cf1667-f1f0-4921-827d-d56af46afdaa",
    "questionCount": 10,
    "status": "Generated"
  }
}
```

### Validation Rules
- `title` required.
- `questionCount` positive and bounded.
- `passingScore` between 0 and 100.

### Business Rules
- AI token balance required.
- Generated quiz contains questions and answers.

### Error Responses
- `400`, `403`, `404`, `409`, `429`

# Get Quiz

### API Name
Get Quiz Detail

### Endpoint
`GET /api/quizzes/{quizId}`

### Method
`GET`

### Authorization
Owner, Shared User, Admin

### Description
Return quiz detail with questions and answer options.

### Response DTO

```json
{
  "success": true,
  "data": {
    "quizId": "12cf1667-f1f0-4921-827d-d56af46afdaa",
    "title": "AI Basics Quiz",
    "description": "Auto-generated quiz from notes",
    "timeLimitMinutes": 15,
    "passingScore": 70,
    "questions": [
      {
        "questionId": "ce91704a-b824-4db3-b0bb-a54ccfefdd10",
        "text": "What does AI stand for?",
        "type": "SingleChoice",
        "sortOrder": 1,
        "points": 1,
        "answers": [
          {
            "answerId": "d68c16c4-c756-4134-b30a-6795a31daa8c",
            "text": "Artificial Intelligence",
            "sortOrder": 1
          }
        ]
      }
    ]
  }
}
```

### Validation Rules
- `quizId` required.

### Business Rules
- During attempt mode, correct answers should be omitted.

### Error Responses
- `403`, `404`

# Submit Quiz

### API Name
Submit Quiz

### Endpoint
`POST /api/quizzes/{quizId}/submissions`

### Method
`POST`

### Authorization
Student, Educator, Admin

### Description
Submit answers for scoring and record the result.

### Request DTO

```json
{
  "answers": [
    {
      "questionId": "ce91704a-b824-4db3-b0bb-a54ccfefdd10",
      "selectedAnswerIds": [
        "d68c16c4-c756-4134-b30a-6795a31daa8c"
      ],
      "textAnswer": null
    }
  ]
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "submissionId": "36cc24d7-a4d8-49fe-95d8-d767340b168a",
    "score": 80,
    "passed": true,
    "submittedAt": "2026-06-09T12:00:00Z"
  }
}
```

### Validation Rules
- `answers` required.
- Question ids must belong to quiz.

### Business Rules
- One submission row per attempt.
- Detailed answer history is not currently supported by schema, only aggregate score.

### Error Responses
- `400`, `403`, `404`, `422`

# Get Quiz Result

### API Name
Get Quiz Result

### Endpoint
`GET /api/quizzes/submissions/{submissionId}`

### Method
`GET`

### Authorization
Submission Owner, Admin

### Description
Return result summary for a quiz submission.

### Response DTO

```json
{
  "success": true,
  "data": {
    "submissionId": "36cc24d7-a4d8-49fe-95d8-d767340b168a",
    "quizId": "12cf1667-f1f0-4921-827d-d56af46afdaa",
    "score": 80,
    "submittedAt": "2026-06-09T12:00:00Z",
    "passed": true
  }
}
```

### Validation Rules
- `submissionId` required.

### Business Rules
- Full answer review requires additional submission detail entity.

### Error Responses
- `403`, `404`

# Quiz History

### API Name
Get Quiz History

### Endpoint
`GET /api/quizzes/history`

### Method
`GET`

### Authorization
Student, Educator, Admin

### Description
Return current user's quiz attempts.

### Response DTO

```json
{
  "success": true,
  "data": [
    {
      "submissionId": "36cc24d7-a4d8-49fe-95d8-d767340b168a",
      "quizId": "12cf1667-f1f0-4921-827d-d56af46afdaa",
      "quizTitle": "AI Basics Quiz",
      "score": 80,
      "submittedAt": "2026-06-09T12:00:00Z"
    }
  ]
}
```

### Validation Rules
- Auth required.

### Business Rules
- Limited to the current user unless admin query endpoint is added.

### Error Responses
- `401` unauthorized

## VOTE MODULE

# Upvote

### API Name
Upvote Document

### Endpoint
`POST /api/documents/{documentId}/votes/upvote`

### Method
`POST`

### Authorization
Student, Educator, Admin

### Description
Create or update vote to `Upvote`.

### Response DTO

```json
{
  "success": true,
  "data": {
    "documentId": "6027360e-d010-4a5c-9fe9-d4f9efee60fd",
    "voteType": "Upvote"
  }
}
```

### Validation Rules
- `documentId` required.

### Business Rules
- One vote per user per document.

### Error Responses
- `403`, `404`

# Downvote

### API Name
Downvote Document

### Endpoint
`POST /api/documents/{documentId}/votes/downvote`

### Method
`POST`

### Authorization
Student, Educator, Admin

### Description
Create or update vote to `Downvote`.

### Response DTO

```json
{
  "success": true,
  "data": {
    "documentId": "6027360e-d010-4a5c-9fe9-d4f9efee60fd",
    "voteType": "Downvote"
  }
}
```

### Validation Rules
- `documentId` required.

### Business Rules
- One vote per user per document.

### Error Responses
- `403`, `404`

# Remove Vote

### API Name
Remove Vote

### Endpoint
`DELETE /api/documents/{documentId}/votes`

### Method
`DELETE`

### Authorization
Student, Educator, Admin

### Description
Remove current user's vote from a document.

### Response DTO

```json
{
  "success": true,
  "message": "Vote removed successfully."
}
```

### Validation Rules
- `documentId` required.

### Business Rules
- No-op delete may still return success.

### Error Responses
- `403`, `404`

## REPORT MODULE

# Report Document

### API Name
Create Report

### Endpoint
`POST /api/documents/{documentId}/reports`

### Method
`POST`

### Authorization
Student, Educator, Admin

### Description
Report a document for moderation review.

### Request DTO

```json
{
  "reason": "Copyright issue",
  "details": "This document appears to reuse proprietary content."
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "reportId": "de30108a-7db7-49bf-85f7-b4989702c346",
    "status": "Pending"
  }
}
```

### Validation Rules
- `reason` required, max 200 chars.
- `details` optional, max 2000 chars.

### Business Rules
- Duplicate active report prevention is recommended.

### Error Responses
- `400`, `403`, `404`, `409`

# Get Reports

### API Name
List Reports

### Endpoint
`GET /api/reports`

### Method
`GET`

### Authorization
Admin

### Description
Return paged report list for moderation.

### Response DTO

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "reportId": "de30108a-7db7-49bf-85f7-b4989702c346",
        "documentId": "6027360e-d010-4a5c-9fe9-d4f9efee60fd",
        "documentTitle": "AI Notes",
        "reportedByUserId": "c3d8bd3d-fdc7-4d85-b4bf-4fe4b074859f",
        "reason": "Copyright issue",
        "status": "Pending",
        "createdAt": "2026-06-09T12:15:00Z"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  }
}
```

### Validation Rules
- Support filters by status and date range.

### Business Rules
- Admin-only visibility.

### Error Responses
- `401`, `403`

# Resolve Report

### API Name
Resolve Report

### Endpoint
`PATCH /api/reports/{reportId}/status`

### Method
`PATCH`

### Authorization
Admin

### Description
Update report review status.

### Request DTO

```json
{
  "status": "Resolved",
  "resolutionNote": "Document archived after review."
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Report status updated successfully."
}
```

### Validation Rules
- `status` must be `Reviewed`, `Resolved`, or `Rejected`.

### Business Rules
- Valid transitions should be enforced.
- Resolution may trigger notifications.

### Error Responses
- `400`, `404`, `409`

## NOTIFICATION MODULE

# Get Notifications

### API Name
List Notifications

### Endpoint
`GET /api/notifications`

### Method
`GET`

### Authorization
Student, Educator, Admin

### Description
Return current user's notifications.

### Response DTO

```json
{
  "success": true,
  "data": {
    "unreadCount": 2,
    "items": [
      {
        "notificationId": "fb0ebcb8-f6d9-43ba-8f5f-0d4858b8d78c",
        "title": "Payment completed",
        "message": "Your Premium plan payment has completed.",
        "type": "Payment",
        "isRead": false,
        "createdAt": "2026-06-09T12:20:00Z"
      }
    ]
  }
}
```

### Validation Rules
- Optional type and read filters.

### Business Rules
- Notifications are scoped to current user.

### Error Responses
- `401` unauthorized

# Mark As Read

### API Name
Mark Notification As Read

### Endpoint
`PATCH /api/notifications/{notificationId}/read`

### Method
`PATCH`

### Authorization
Student, Educator, Admin

### Description
Mark one notification as read.

### Response DTO

```json
{
  "success": true,
  "message": "Notification marked as read."
}
```

### Validation Rules
- `notificationId` required.

### Business Rules
- Idempotent operation.

### Error Responses
- `403`, `404`

# Mark All As Read

### API Name
Mark All Notifications As Read

### Endpoint
`PATCH /api/notifications/read-all`

### Method
`PATCH`

### Authorization
Student, Educator, Admin

### Description
Mark all current user's notifications as read.

### Response DTO

```json
{
  "success": true,
  "message": "All notifications marked as read."
}
```

### Validation Rules
- Auth required.

### Business Rules
- Bulk update current user's rows only.

### Error Responses
- `401` unauthorized

## ADMIN MODULE

# Dashboard

### API Name
Get Admin Dashboard

### Endpoint
`GET /api/admin/dashboard`

### Method
`GET`

### Authorization
Admin

### Description
Return top-level operational metrics.

### Response DTO

```json
{
  "success": true,
  "data": {
    "totalUsers": 1200,
    "activeUsers": 1100,
    "totalDocuments": 5400,
    "publishedDocuments": 4200,
    "pendingReports": 12,
    "pendingPayments": 4,
    "monthlyRevenue": 25000000
  }
}
```

### Validation Rules
- Admin auth required.

### Business Rules
- Values are aggregated summaries.

### Error Responses
- `401`, `403`

# Manage Users

### API Name
Search Users

### Endpoint
`GET /api/admin/users`

### Method
`GET`

### Authorization
Admin

### Description
Search and filter user accounts.

### Response DTO

```json
{
  "success": true,
  "data": {
    "items": [
      {
        "userId": "c3d8bd3d-fdc7-4d85-b4bf-4fe4b074859f",
        "fullName": "Nguyen Van A",
        "email": "student@example.com",
        "role": "Student",
        "status": "Active",
        "isActive": true,
        "createdAt": "2026-05-01T00:00:00Z"
      }
    ]
  }
}
```

### Validation Rules
- Support keyword, role, and status filters.

### Business Rules
- Admin can view all users.

### Error Responses
- `401`, `403`

### API Name
Update User Status

### Endpoint
`PATCH /api/admin/users/{userId}/status`

### Method
`PATCH`

### Authorization
Admin

### Description
Activate, deactivate, or otherwise adjust user status.

### Request DTO

```json
{
  "status": "Suspended",
  "isActive": false
}
```

### Response DTO

```json
{
  "success": true,
  "message": "User status updated successfully."
}
```

### Validation Rules
- `status` required.

### Business Rules
- Cannot deactivate the last active admin.

### Error Responses
- `400`, `404`, `409`

# Manage Documents

### API Name
Admin List Documents

### Endpoint
`GET /api/admin/documents`

### Method
`GET`

### Authorization
Admin

### Description
Search all documents across the platform.

### Response DTO
Paginates the same document search projection with owner information.

### Validation Rules
- Standard pagination and filters.

### Business Rules
- Admin can see all statuses.

### Error Responses
- `401`, `403`

### API Name
Admin Update Document Status

### Endpoint
`PATCH /api/admin/documents/{documentId}/status`

### Method
`PATCH`

### Authorization
Admin

### Description
Moderate a document by changing status or moderation flags.

### Request DTO

```json
{
  "status": "Archived",
  "reason": "Policy violation"
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Document status updated successfully."
}
```

### Validation Rules
- `status` required.

### Business Rules
- Moderate actions may notify owner.

### Error Responses
- `400`, `404`

# Manage Reports

### API Name
Admin Report Queue

### Endpoint
`GET /api/admin/reports`

### Method
`GET`

### Authorization
Admin

### Description
Alias or specialized queue view of reports with moderation metrics.

### Response DTO
Can reuse `/api/reports` response shape.

### Validation Rules
- Admin required.

### Business Rules
- Consider consolidating with report module admin endpoints.

### Error Responses
- `401`, `403`

# Manage Payments

### API Name
Admin List Payments

### Endpoint
`GET /api/admin/payments`

### Method
`GET`

### Authorization
Admin

### Description
Search all payment transactions.

### Response DTO
Paged payment projections with user and tier metadata.

### Validation Rules
- Standard pagination and status filter.

### Business Rules
- Sensitive provider identifiers should be protected.

### Error Responses
- `401`, `403`

### API Name
Admin Update Payment Status

### Endpoint
`PATCH /api/admin/payments/{paymentId}/status`

### Method
`PATCH`

### Authorization
Admin

### Description
Manual review or correction of payment status.

### Request DTO

```json
{
  "status": "Refunded",
  "note": "Manual refund approved"
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Payment status updated successfully."
}
```

### Validation Rules
- Valid status required.

### Business Rules
- Manual overrides should be auditable.
- Refund may require quota recalculation.

### Error Responses
- `400`, `404`, `409`

# Manage Membership Tiers

### API Name
Admin List Tiers

### Endpoint
`GET /api/admin/tiers`

### Method
`GET`

### Authorization
Admin

### Description
List all membership tiers.

### Response DTO

```json
{
  "success": true,
  "data": [
    {
      "tierId": "4f9e8fd0-76e3-4dc3-9aeb-5e31dbca74a4",
      "tierName": "Premium",
      "storageLimitMb": 1024,
      "aiTokens": 5000
    }
  ]
}
```

### Validation Rules
- Admin auth required.

### Business Rules
- Tiers should also include pricing in future data model.

### Error Responses
- `401`, `403`

### API Name
Admin Create Tier

### Endpoint
`POST /api/admin/tiers`

### Method
`POST`

### Authorization
Admin

### Description
Create a new membership tier.

### Request DTO

```json
{
  "tierName": "Premium",
  "storageLimitMb": 1024,
  "aiTokens": 5000
}
```

### Response DTO

```json
{
  "success": true,
  "data": {
    "tierId": "4f9e8fd0-76e3-4dc3-9aeb-5e31dbca74a4"
  }
}
```

### Validation Rules
- `tierName` required.
- `storageLimitMb` non-negative.
- `aiTokens` non-negative.

### Business Rules
- Tier name should be unique.

### Error Responses
- `400`, `409`

### API Name
Admin Update Tier

### Endpoint
`PUT /api/admin/tiers/{tierId}`

### Method
`PUT`

### Authorization
Admin

### Description
Update membership tier limits.

### Request DTO

```json
{
  "tierName": "Premium Plus",
  "storageLimitMb": 2048,
  "aiTokens": 10000
}
```

### Response DTO

```json
{
  "success": true,
  "message": "Tier updated successfully."
}
```

### Validation Rules
- Same as create.

### Business Rules
- Existing users may require recalculated quota state.

### Error Responses
- `400`, `404`, `409`

### API Name
Admin Delete Tier

### Endpoint
`DELETE /api/admin/tiers/{tierId}`

### Method
`DELETE`

### Authorization
Admin

### Description
Delete membership tier if not in active use.

### Response DTO

```json
{
  "success": true,
  "message": "Tier deleted successfully."
}
```

### Validation Rules
- `tierId` required.

### Business Rules
- Prevent deleting tiers referenced by active users or payment history.

### Error Responses
- `404`, `409`

# Implementation Roadmap

## Phase 1 (MVP)

### APIs to Build
- Auth: register, login, refresh token, logout, change password
- User: get profile, update profile, storage usage
- Subject: list, detail, admin CRUD
- Document: upload, update, delete, get detail, my documents, public documents, search
- Basic admin: dashboard-lite, user list, document list

### Dependencies
- JWT auth configuration
- File storage provider
- ASP.NET Identity integration
- Subject and document repositories/services
- Basic role-based authorization

### Risks
- File storage strategy not yet defined
- Public/private document visibility rule not fully modeled
- `User.TierId` and `TierUser` dual membership representation may cause ambiguity

### Priority
Highest. This phase enables core user onboarding and the primary document workflow.

## Phase 2

### APIs to Build
- Document chunk generation and inspection
- AI chat session APIs
- Flashcard generation and CRUD
- Quiz generation, retrieval, submission, result, history
- Voting APIs
- Reporting APIs
- Notification APIs

### Dependencies
- AI provider abstraction
- Background job processing for chunking and generation
- Token consumption service
- Search/indexing improvements

### Risks
- AI costs and latency
- Need for async job orchestration
- Quiz submission detail schema is incomplete for answer review

### Priority
High. This phase delivers the differentiated AI learning value of the platform.

## Phase 3

### APIs to Build
- Subscription APIs
- Payment APIs and gateway callbacks
- Advanced admin payment and tier management
- Document sharing APIs
- Expanded moderation workflows

### Dependencies
- Pricing model for tiers
- Payment gateway integration
- Share/permission data model
- Audit logging

### Risks
- Sharing cannot be fully implemented without new entities
- Payment and subscription lifecycle lacks validity periods and pricing entities
- Refund and downgrade logic remain undefined

### Priority
Medium to High. Essential for monetization and collaboration, but dependent on missing schema pieces.

# Architecture Review

# Architectural Issues

1. `User.TierId` and `TierUser` overlap in responsibility.
   - `User.TierId` suggests a direct current tier pointer.
   - `TierUser` suggests a tier assignment history table.
   - Without explicit rules, current tier determination becomes ambiguous.

2. `Document` lacks explicit visibility or share status fields.
   - The requested API scope includes public, shared, and my documents.
   - Current model only has `DocumentStatus`, which is insufficient for access policy.

3. `ChatMessage.Role` is a raw string.
   - This risks inconsistent values such as `user`, `User`, `assistant`, `system`.
   - It should likely be enum-backed.

4. `Notification` lacks reference metadata.
   - No deep link target, related entity id, or action payload exists.

# Design Flaws

1. `TierMembership` contains quota data but no pricing.
   - Upgrade and payment flows need price, billing cycle, and currency source.

2. `TierUser` has no effective dates.
   - No `StartAt`, `EndAt`, `IsActive`, or `SourcePaymentId` fields.
   - Tier history is therefore weak and active subscription cannot be reliably inferred.

3. `QuizSubmission` is too minimal.
   - Only total score and submitted time are stored.
   - No per-question user answers, correctness breakdown, duration, or attempt number.

4. `DocumentChunk` stores JSON strings rather than typed structured data.
   - This is flexible but weak for validation, querying, and provider portability.

5. `Payment` has no callback audit fields.
   - Missing paid time, raw payload, failure reason, and idempotency fields.

# Technical Risks

1. AI token accounting may become inconsistent if decrement operations are not transactional.
2. Storage quota can drift if file size tracking and deletion cleanup are not coordinated.
3. Heavy AI generation on request/response endpoints may cause timeouts.
4. Search performance may degrade without full-text or indexed search strategy.
5. Payment callbacks require idempotency and signature validation from day one.

# Missing Entities

1. `DocumentShare`
   - Needed for document sharing, revoke share, and shared-document listing.
   - Suggested fields: `Id`, `DocumentId`, `SharedWithUserId`, `Permission`, `CreatedBy`, `ExpiresAt`.

2. `SubscriptionPlanPrice` or pricing fields on `TierMembership`
   - Needed for billing amount calculation.

3. `PaymentAudit` or payment raw callback storage
   - Needed for compliance, troubleshooting, and gateway reconciliation.

4. `QuizSubmissionAnswer`
   - Needed to persist user answers and enable detailed result review.

5. `DocumentAccessPolicy` or fields on `Document`
   - Needed for public/private/shared visibility.

6. `PasswordResetToken` or equivalent support model
   - Needed for forgot-password flow if not delegated fully to Identity token providers.

7. `AuditLog`
   - Strongly recommended for admin actions and payment/subscription changes.

# Missing APIs

1. Email verification APIs
2. Resend verification email APIs
3. Download document or secure file access APIs
4. Batch delete or archive document APIs
5. Admin audit log APIs
6. Notification preference APIs
7. Webhook retry or reconciliation APIs for payment providers
8. AI token usage history APIs
9. Storage cleanup/recalculation admin APIs

# Scalability Risks

1. Large document ingestion may overload synchronous upload and processing endpoints.
2. Chat history can grow rapidly without pagination.
3. Flashcard and quiz retrieval can become expensive without projection optimization.
4. Document search on title/description only may not scale without indexing.
5. JSON embeddings stored inside transactional database may increase storage pressure significantly.

# Security Risks

1. File URLs stored directly may expose documents if object storage permissions are weak.
2. Payment callback spoofing risk if signatures are not enforced.
3. Missing ownership checks on content-generated resources would leak data across users.
4. Missing audit trail for admin actions creates operational risk.
5. Prompt injection and document exfiltration risk in AI chat must be explicitly mitigated.

# Performance Risks

1. N+1 query risk on document detail with votes, reports, flashcards, quizzes, and chunks.
2. Bulk mark-as-read and notification feed need efficient indexing.
3. Regeneration operations may repeatedly reprocess the same document without caching.
4. Storing large embeddings in the main relational database may slow backup, restore, and read performance.

# Recommended Next Architectural Decisions

1. Decide the canonical active subscription model.
   - Option A: Keep `User.TierId` as active tier pointer and `TierUser` as history.
   - Option B: Remove `User.TierId` and derive active tier from a richer `TierUser` record.

2. Add explicit document visibility model.
   - Example: `Visibility` enum with `Private`, `Shared`, `Public`.

3. Add pricing and subscription duration to membership tiers.

4. Add answer-detail persistence for quiz submissions.

5. Move long-running AI generation to asynchronous jobs.

6. Introduce audit logging and event-driven notification generation.

# Frontend Integration Guidance

## Suggested Screen Map

### Public Screens
- Login
- Register
- Forgot Password
- Subject Catalog
- Public Documents
- Public Document Detail

### User Screens
- My Profile
- Storage Usage
- My Documents
- Upload Document
- Document Detail
- Shared Documents
- AI Chat Session
- Flashcards
- Quiz Detail
- Quiz Result
- Payment History
- Current Tier
- Upgrade Tier
- Notifications

### Admin Screens
- Dashboard
- User Management
- Document Management
- Report Queue
- Payment Management
- Tier Management
- Subject Management

## Recommended API Delivery Order for Frontend Team

1. Auth + Profile + Subject list
2. Upload document + My documents + Document detail
3. Public documents + Search
4. Flashcards + Quiz + Chat
5. Notifications + Reports + Votes
6. Payments + Subscriptions + Admin panels

## Final Conclusion

The current entity model already supports a strong foundation for an AI-powered learning document platform centered around document ingestion, AI-assisted study material generation, quizzes, chat, voting, reporting, and subscriptions. However, several important business capabilities required by the requested scope are only partially modeled or not modeled yet, especially document sharing, tier pricing, subscription validity, quiz answer details, and operational auditing.

Frontend developers can use this document immediately to design screens and client contracts, but backend implementation should first resolve the highlighted architectural ambiguities to avoid breaking API contracts later.
