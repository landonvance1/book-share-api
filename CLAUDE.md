# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Application Overview

BookSharingWebAPI is the backend for a **community-based book lending platform** that enables users to share physical books with others in their communities. The API facilitates the entire borrowing lifecycle from discovery to return, with built-in chat, notifications, and dispute resolution.

**Core Value Proposition:**
- Discover books available in your communities
- Request to borrow books from other members
- Track lending/borrowing status through a structured workflow
- Communicate with lenders/borrowers via real-time chat
- Manage your personal library and share availability

**Technology Stack:**
- **ASP.NET Core 8.0** - Minimal APIs pattern
- **PostgreSQL 15** - Relational database (via Docker)
- **Entity Framework Core 9.0.8** - ORM with Code-First migrations
- **ASP.NET Core Identity** - User management and authentication
- **JWT Authentication** - Access tokens (24h) + refresh tokens (7d)
- **SignalR** - WebSocket-based real-time messaging
- **OpenLibrary API** - External book search integration
- **Docker & Docker Compose** - Containerized deployment

## Development Commands

### Building and Running
- `dotnet build BookSharingApp.csproj` - Build the application (use specific project file due to .sln presence)
- `dotnet run` - Run the application (starts on https://localhost:7061 and http://localhost:5155)
- `dotnet run --launch-profile http` - Run with HTTP only
- `dotnet run --launch-profile https` - Run with HTTPS (default)

### Testing

**Test Framework:** xUnit with EF Core In-Memory Database Provider + Moq + FluentAssertions

**Running Tests:**
- `dotnet test BookSharingApp.Tests/BookSharingApp.Tests.csproj` - Run unit tests (default — use this unless told otherwise)
- `dotnet test WebAPI.sln` - Run **all** tests including integration tests (see warning below)
- `dotnet test BookSharingApp.Tests/BookSharingApp.Tests.csproj --filter "FullyQualifiedName~CreateShareAsync"` - Run specific test class
- `dotnet test BookSharingApp.Tests/BookSharingApp.Tests.csproj --logger "console;verbosity=detailed"` - Verbose test output

**Test Projects:**
- `BookSharingApp.Tests/` — Unit tests (fast, free, safe to run anytime)
- `BookSharingApp.IntegrationTests/` — Integration tests that hit **real external services** (OpenLibrary)

**IMPORTANT — Integration tests are expensive:**
Do NOT run `BookSharingApp.IntegrationTests` unless changes directly affect the integration-tested code paths (e.g., `BookCoverAnalysisService`, `OpenLibraryService`, or their models/interfaces). These tests make real API calls that cost money. When in doubt, run only the unit tests.

#### Testing Philosophy

This project uses **xUnit with EF Core In-Memory Database Provider** for unit testing service layer logic. Tests execute real LINQ queries against an in-memory database rather than mocking DbContext.

**Mock Strategy:**
- **ApplicationDbContext:** EF Core In-Memory provider (real DbContext, fake database)
- **INotificationService:** Mocked with Moq to verify notification creation calls
- **ILogger<T>:** Mocked with Moq for dependency injection

#### Test Organization

Tests are organized using a **single file per service/class** pattern with nested test classes for each method:

```
BookSharingApp.Tests/
├── Helpers/
│   ├── DbContextHelper.cs          # Creates isolated in-memory DbContext instances
│   ├── TestDataBuilder.cs          # Fluent API for creating test entities
│   └── MockHttpMessageHandlerHelper.cs  # Helper for mocking HTTP responses
├── Services/
│   ├── ChatServiceTests.cs
│   ├── NotificationServiceTests.cs
│   ├── ShareServiceTests.cs
│   ├── TokenServiceTests.cs
│   └── OpenLibraryServiceTests.cs
└── Validators/
    └── ShareStatusValidatorTests.cs
```

**Current test coverage:** Run `dotnet test WebAPI.sln` to see the latest test counts.

**Test File Structure Pattern:**
Each service test file contains:
1. **Base class** with shared mock setup (`ServiceNameTestBase : IDisposable`)
2. **Nested classes** for each method being tested (inherit from base class)
3. **Protected properties** for mocks (accessible to all nested test classes)

**Test Naming Convention:** `MethodName_Scenario_ExpectedBehavior`

Examples:
- **Service tests:** `CreateShareAsync_WithValidRequest_CreatesShareWithRequestedStatus`
- **Service tests:** `CreateShareAsync_WhenBorrowingOwnBook_ThrowsInvalidOperationException`
- **Service tests:** `SendMessageAsync_WithValidMessage_CreatesMessageAndNotification`
- **Validator tests:** `ValidateStatusTransition_WhenBorrowerTriesToSetReady_ReturnsFailure`
- **Validator tests:** `ValidateStatusTransition_FromRequestedToReady_ReturnsSuccess`

#### Test Helper Utilities

**DbContextHelper** (`BookSharingApp.Tests/Helpers/DbContextHelper.cs`)

Provides factory methods for creating isolated in-memory database contexts:

```csharp
// Auto-isolated context (unique GUID database name per test)
using var context = DbContextHelper.CreateInMemoryContext();

// Named context (for sharing state across multiple context instances in same test)
using var context = DbContextHelper.CreateInMemoryContext("my-test-db");
```

Each test gets a completely isolated database instance to prevent data leakage between tests.

**TestDataBuilder** (`BookSharingApp.Tests/Helpers/TestDataBuilder.cs`)

Fluent API for constructing test entities with sensible defaults:

```csharp
var user = TestDataBuilder.CreateUser(id: "user-1", firstName: "John");
var book = TestDataBuilder.CreateBook(title: "The Great Gatsby", author: "F. Scott Fitzgerald");
var userBook = TestDataBuilder.CreateUserBook(userId: user.Id, bookId: book.Id, status: UserBookStatus.Available);
var community = TestDataBuilder.CreateCommunity(name: "Book Club");
var share = TestDataBuilder.CreateShare(userBookId: userBook.Id, borrower: borrower.Id, status: ShareStatus.Requested);
```
#### Test Isolation Strategy

Each test receives a **unique in-memory database** instance via GUID-based naming in `DbContextHelper.CreateInMemoryContext()`. This ensures:
- No data leakage between tests
- Tests can run in parallel safely
- No cleanup required (garbage collected automatically)
- Fast execution (in-memory only)


### Package Management
- `dotnet restore` - Restore NuGet packages
- `dotnet add package <PackageName>` - Add a new package

### Local Development Setup
For local PostgreSQL development without Docker, set up user secrets for secure credential storage:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=booksharingdb;Username=bookuser;Password=YOUR_LOCAL_PASSWORD"
dotnet user-secrets set "JWT:Key" "YOUR_JWT_SECRET_KEY"
```

Replace `YOUR_LOCAL_PASSWORD` with your local PostgreSQL password and `YOUR_JWT_SECRET_KEY` with a secure 32+ character key for JWT token signing. This keeps credentials out of source control while allowing local development.

**Prerequisites for local development:**
1. Install PostgreSQL locally
2. Connect as postgres superuser: `sudo -i -u postgres psql`
3. Create database: `CREATE DATABASE booksharingdb;`
4. Create user: `CREATE USER bookuser WITH PASSWORD 'your_password';`
5. Grant database permissions: `GRANT ALL PRIVILEGES ON DATABASE booksharingdb TO bookuser;`
6. Connect to the database: `\c booksharingdb`
7. Grant schema permissions (required for PostgreSQL 15+):
   ```sql
   GRANT USAGE, CREATE ON SCHEMA public TO bookuser;
   GRANT ALL PRIVILEGES ON SCHEMA public TO bookuser;
   ALTER SCHEMA public OWNER TO bookuser;
   ```
8. Exit PostgreSQL: `\q`
9. Set user secrets (commands above)
10. Run with `dotnet run`

### Docker Commands

#### First Time Setup
- `cp .env.example .env` - Copy environment template (required for local development)
- Edit `.env` file with your preferred database credentials

#### Running the Application
- `docker build -t booksharing-api .` - Build Docker image
- `docker run -p 3001:8080 booksharing-api` - Run container on port 3001
- `docker-compose up` - Start with docker-compose (port 3001)
- `docker-compose up --build` - Rebuild and start containers
- `docker-compose down` - Stop and remove containers

## Architecture

### Project Structure
```
BookSharingWebAPI/
├── BookSharingApp.Cli/    # Admin CLI tool (report viewer)
│   ├── Commands/          # Command implementations
│   └── Formatting/        # Table output helpers
├── BookSharingApp.Tests/  # Unit test project (xUnit)
│   ├── Helpers/           # Test utilities
│   ├── Services/          # Service layer tests
│   └── Validators/        # Validator tests
├── Common/                # Shared constants and enums
├── Data/                  # EF Core DbContext and seeding
├── Endpoints/             # Minimal API endpoint definitions
├── Hubs/                  # SignalR hubs (ChatHub)
├── Middleware/            # Custom middleware (rate limiting)
├── Migrations/            # EF Core migrations (~26 files)
├── Models/                # Entity models and DTOs
├── Services/              # Business logic layer
├── Validators/            # Business rule validators
└── wwwroot/images/        # Book cover thumbnails
```

### Key Components

1. **Minimal API Architecture**: Uses ASP.NET Core Minimal APIs with static extension methods for endpoint mapping
2. **Service Layer Pattern**: Business logic separated into service classes (ShareService, NotificationService, etc.)
3. **Entity Framework Core**: PostgreSQL database with Code-First migrations
4. **Dependency Injection**: Services, DbContext, and repositories registered in DI container
5. **Swagger Integration**: Configured for development environment with OpenAPI documentation
6. **SignalR Hub**: Real-time WebSocket communication for chat functionality
7. **Structured JSON Logging**: All logs emitted as newline-delimited JSON via the built-in `AddJsonConsole` formatter (no extra dependencies). Every entry carries a `RequestId` scope (from `HttpContext.TraceIdentifier`) that correlates all service-level logs for a single request. HTTP method, path, status code, and duration are captured per request via `HttpLogging` middleware. All existing log statements already use named placeholders (`{ShareId}`, `{UserId}`, etc.) which appear as queryable fields in the JSON output. Log levels: `Debug` default in development, `Information` in production.

### Data Flow
- HTTP requests → Endpoints → Services → DbContext → PostgreSQL
- Real-time chat: SignalR Hub → ChatHub → Broadcast to connected clients
- Authentication: JWT middleware validates tokens before reaching endpoints

## Core Entities & Domain Model

### User
- Unique identifier (GUID)
- Profile: email, firstName, lastName, fullName
- Authentication via ASP.NET Core Identity with JWT tokens
- Can join multiple communities
- Managed by ASP.NET Core Identity tables
- **Account deletion (tombstone pattern):** `DELETE /auth/account` scrubs all PII in-place rather than deleting the row, preserving FK integrity across shares, chat messages, and notifications. The `aspnetusers` row is kept with name set to "[Deleted User]", email/password nulled, and account permanently locked. Chat message content is replaced with "[deleted]". No schema migration required.

### Book
- Basic book information: id, title, author, thumbnailUrl
- Unique constraint on (title, author) combination
- Thumbnails downloaded from OpenLibrary or stored locally in wwwroot/images/
- Multiple users can own the same book

### UserBook (Library Entry)
- Links User to Book (ownership relationship)
- **Status enum**: Available(1), BeingShared(2), Unavailable(3)
- Users manage their personal library of books
- Only available books can be requested by others

### Share (Lending Transaction)
The core entity representing a book-sharing relationship between lender and borrower.

**Key Properties:**
- `userBookId` - The lender's book being shared
- `borrower` - User ID of the borrower
- `returnDate` - Expected return date (set by lender)
- `status` - Current workflow state

**ShareStatus Workflow:**
1. **Requested** - Borrower requests to borrow → Lender gets notification
2. **Ready** - Lender approves → Book ready for pickup
3. **PickedUp** - Borrower confirms pickup → Book in borrower's possession
4. **Returned** - Borrower returns book → Awaiting lender confirmation
5. **HomeSafe** - Lender confirms receipt → Transaction complete ✓

**Alternative Paths:**
- **Declined** - Lender rejects request (terminal state)
- **Disputed** - Either party raises issue at any stage (terminal state)

**Authorization:**
- Lender-only actions: Ready, HomeSafe, Declined, SetReturnDate
- Borrower-only actions: PickedUp, Returned
- Either party: Disputed, Archive/Unarchive

**Validation Rules:**
- Cannot borrow own books
- Must share a community with book owner
- No duplicate active shares for same book by the same borrower
- Cannot skip workflow stages or move backwards
- Only available books can be requested
- Only terminal states (HomeSafe, Disputed, Declined) can be archived

### ShareUserState (Archive State)
- Tracks per-user archive status for shares
- Allows borrower and lender to independently archive completed shares
- Only terminal states can be archived

### Community
- Groups of users who share books with each other
- Users can join multiple communities
- Book searches are scoped to shared communities (privacy by design)
- Users limited to creating 5 communities
- Creator automatically becomes moderator
- Community deleted when last member leaves

### ChatMessage & ChatThread
- Each share has a dedicated chat thread
- Real-time messaging via SignalR WebSocket
- Message types: user messages + system messages (status changes)
- Rate limited: 30 messages per 2 minutes per user
- Paginated message history (50 per page, max 100)

### ChatMessageReport
Stores reports submitted by share participants about inappropriate chat messages.

**Key Properties:**
- `MessageId` — the reported message
- `ReporterId` / `ReportedUserId` — who filed the report and who was reported
- `Category` — enum: Spam, Harassment, InappropriateContent, Other
- `ReportedContent` — snapshot of the message text at report time (max 2000 chars)
- `Notes` — optional free-text from the reporter (max 500 chars)
- `CreatedAt` — UTC timestamp
- `IsResolved` — binary flag (`boolean NOT NULL DEFAULT false`) set by admins once the report has been handled

**Constraints:**
- Unique index on `(MessageId, ReporterId)` prevents duplicate reports from the same user on the same message
- Users cannot report their own messages

### Notification
- **Types**: ShareStatusChanged, ShareDueDateChanged, ShareMessageReceived, UserBookWithdrawn, AdminWarning
- Separate read tracking for share vs. chat notifications
- Includes share details, message content, and actor information
- Auto-created when share status changes or messages are sent
- Persists even if share is archived
- **AdminWarning** — sent by admins via CLI to warn a user about their behavior. Optionally scoped to a share. `CreatedByUserId` is a self-reference (the warned user's own ID) because the CLI has no authenticated admin identity. Mobile client should display this type as a blocking modal and should not show the "created by" field.

## API Endpoint Reference

**Note:** All endpoints except `/auth/*` require a valid JWT token in the Authorization header: `Authorization: Bearer <token>`

### Authentication (`/auth`)
- `POST /auth/register` - Create account
- `POST /auth/login` - Authenticate user
- `POST /auth/refresh` - Refresh access token
- `DELETE /auth/account` - Permanently delete the authenticated user's account (Apple App Store compliant)

### Books (`/books`)
- `GET /books` - List all books
- `GET /books/{id}` - Get book by ID
- `POST /books?addToUser={bool}` - Add new book (downloads thumbnail from OpenLibrary)
- `GET /books/search?title={}&author={}&includeExternal={bool}` - Search (local + OpenLibrary)
- `GET /books/isbn/{isbn}` - Lookup by ISBN (OpenLibrary)

### User Books / Library (`/user-books`)
- `GET /user-books/user/{userId}` - Get user's library
- `POST /user-books` - Add book to library (body: bookId)
- `PUT /user-books/{id}/status` - Update availability status
- `DELETE /user-books/{id}` - Remove from library
- `GET /user-books/search?search={query}` - Search accessible books in communities

### Shares (`/shares`)
- `POST /shares?userbookid={id}` - Request to borrow book
- `GET /shares/borrower` - Get shares as borrower (non-archived)
- `GET /shares/lender` - Get shares as lender (non-archived)
- `GET /shares/borrower/archived` - Get archived borrows
- `GET /shares/lender/archived` - Get archived lends
- `PUT /shares/{id}/status` - Update share status (body: status enum)
- `PUT /shares/{id}/return-date` - Set return date (lender only)
- `POST /shares/{id}/archive` - Archive share (terminal states only)
- `POST /shares/{id}/unarchive` - Unarchive share

### Chat (`/shares/{shareId}/chat`)
- `GET /shares/{shareId}/chat/messages?page={}&pageSize={}` - Get messages (paginated)
- `POST /shares/{shareId}/chat/messages` - Send message (rate limited: 30 per 2 minutes)

### Notifications (`/notifications`)
- `GET /notifications` - Get all unread notifications
- `PATCH /notifications/{id}/read` - Mark a single notification as read by ID (for AdminWarning dismissal)
- `PATCH /notifications/shares/{shareId}/read` - Mark share notifications read
- `PATCH /notifications/shares/{shareId}/chat/read` - Mark chat notifications read

### Communities (`/communities`)
- `GET /communities` - List all communities
- `GET /communities/{id}` - Get community details
- `POST /communities?name={name}` - Create community (limit: 5 per user)
- `DELETE /communities/{id}` - Delete community

### Community Users (`/community-users`)
- `POST /community-users/join/{communityId}` - Join community
- `DELETE /community-users/leave/{communityId}` - Leave community
- `GET /community-users/user/{userId}` - Get user's communities (with member counts)
- `GET /community-users/community/{communityId}` - Get community members

### SignalR Hub (`/chathub`)
**Authentication:** JWT via query string `?access_token={token}`

**Client → Server Methods:**
- `JoinShareChat(shareId)` - Subscribe to share's messages
- `LeaveShareChat(shareId)` - Unsubscribe from share
- `SendMessage(shareId, content)` - Send chat message

**Server → Client Events:**
- `ReceiveMessage(messageDto)` - Broadcast new message to group
- `JoinedChat(shareId)` - Join confirmation
- `LeftChat(shareId)` - Leave confirmation
- `Error(message)` - Error notification

## API Base URLs

### Local Development (without Docker)
- HTTP: `http://localhost:5155`
- HTTPS: `https://localhost:7061`

### Docker
- `http://localhost:3001`

**Example API calls:**
```bash
# Local development
curl http://localhost:5155/books

# Docker
curl http://localhost:3001/books
```

## Database Seeding

The backend includes a DatabaseSeeder with test data for development:
- **5 test users**: user-001 through user-005 (all password: "password")
- **2 communities** with members
- **20+ books** across various genres
- **Sample shares** in different workflow states

## Configuration
- **appsettings.json** - Production configuration
- **appsettings.Development.json** - Development-specific settings
- **launchSettings.json** - Development server profiles and URLs

## Docker Deployment

### Container Configuration
- **Port 8080** - Internal container port (HTTP)
- **Port 3001** - External mapped port
- Static files (wwwroot) are volume-mounted for easy updates

### Docker Files
- **Dockerfile** - Multi-stage build configuration using .NET 8 SDK and runtime
- **.dockerignore** - Excludes build artifacts and unnecessary files from Docker context
- **docker-compose.yml** - Container orchestration for the API and PostgreSQL

### Prerequisites
- Docker Desktop for Windows must be installed to build and run containers
- Create `.env` file from `.env.example` template for local development

### Environment Variables
The application uses environment variables for database configuration to keep credentials secure:
- **POSTGRES_DB** - Database name
- **POSTGRES_USER** - Database username  
- **POSTGRES_PASSWORD** - Database password
- **DB_CONNECTION_STRING** - Full connection string for the application

For local development, copy `.env.example` to `.env` and customize the values as needed.

## Admin CLI Tool

A console application for viewing and managing chat message reports. No new API endpoints — admin access is gated by server/SSH access.

### Running via Docker (interactive)
```bash
docker compose exec -it booksharing-api admin
```
Then run commands at the `>>` prompt. The CLI picks up `ConnectionStrings__DefaultConnection` from the container automatically.

### Running Locally
```bash
dotnet run --project BookSharingApp.Cli -- --connection-string "Host=localhost;Port=5432;Database=booksharingdb;Username=bookuser;Password=YOUR_PASSWORD"
```

### Commands
- **`reports list [--user <name>]`** — List all reports (newest first), optionally filtered by reported user name
- **`reports view <id>`** — Full report detail with share context (book title, lender/borrower, share status)
- **`reports stats`** — Total count, breakdown by category, top reported users

## Related Projects

### Mobile Application (BookSharingApp)
**Location:** `/home/landonlaptop/code/BookSharingApp/`

The React Native mobile application that consumes this API. Built with:
- **React Native 0.81.4** with React 19.1.0
- **Expo SDK 54** for development and builds
- **TypeScript 5.9.2** with strict mode
- **React Navigation 7.x** for navigation
- **React Query 5.90.11** for server state management
- **SignalR 9.0.6** for real-time chat

**Key Features:**
- Book search and discovery within communities
- Personal library management with barcode scanning
- Share request and lifecycle management
- Real-time chat per share
- Push notifications for share updates
- Community management
