# BookShare API

A backend API for a community-based book lending platform that enables users to share physical books with others in their communities. Built with ASP.NET Core 8.0 and PostgreSQL.

## Features

- **Community-based book sharing** — discover and borrow books from members of your communities
- **Structured lending workflow** — request, approve, pickup, return, and confirm through tracked statuses
- **Real-time chat** — per-share messaging via SignalR WebSockets
- **Notifications** — share status changes, due dates, and new messages
- **Book discovery** — search local catalog and OpenLibrary, with ISBN lookup and cover detection
- **Personal library management** — track your books and set availability

## Tech Stack

- **ASP.NET Core 8.0** — Minimal APIs
- **PostgreSQL 15** — via Docker
- **Entity Framework Core 9** — Code-First migrations
- **ASP.NET Core Identity + JWT** — authentication (access + refresh tokens)
- **SignalR** — real-time WebSocket messaging
- **OpenLibrary API** — external book search
- **Docker & Docker Compose** — containerized deployment

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for containerized setup)
- PostgreSQL 15 (for local setup without Docker)

### Quick Start with Docker

```bash
# Clone the repository
git clone <repository-url>
cd book-share-api

# Create environment file
cp .env.example .env
# Edit .env with your preferred credentials

docker-compose up --build
```

The API will be available at `http://localhost:3001`.

### Local Development Setup

1. **Set up PostgreSQL:**

   ```bash
   sudo -i -u postgres psql
   ```

   ```sql
   CREATE DATABASE booksharingdb;
   CREATE USER bookuser WITH PASSWORD 'your_password';
   GRANT ALL PRIVILEGES ON DATABASE booksharingdb TO bookuser;
   \c booksharingdb
   GRANT USAGE, CREATE ON SCHEMA public TO bookuser;
   GRANT ALL PRIVILEGES ON SCHEMA public TO bookuser;
   ALTER SCHEMA public OWNER TO bookuser;
   \q
   ```

2. **Configure user secrets:**

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=booksharingdb;Username=bookuser;Password=your_password"
   dotnet user-secrets set "JWT:Key" "your_secure_32_character_key_here"
   ```

3. **Run the application:**

   ```bash
   dotnet restore
   dotnet run
   ```

   The API starts on `https://localhost:7061` (HTTPS) and `http://localhost:5155` (HTTP).

## API Endpoints

### Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/auth/register` | Create account |
| POST | `/auth/login` | Authenticate user |
| POST | `/auth/refresh` | Refresh access token |
| DELETE | `/auth/account` | Permanently delete account |

### Books

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/books` | List all books |
| GET | `/books/{id}` | Get book by ID |
| POST | `/books?addToUser={bool}` | Add new book |
| GET | `/books/search?title=&author=&includeExternal=` | Search books |
| GET | `/books/isbn/{isbn}` | Lookup by ISBN |

### User Library

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/user-books/user/{userId}` | Get user's library |
| POST | `/user-books` | Add book to library |
| PUT | `/user-books/{id}/status` | Update availability |
| DELETE | `/user-books/{id}` | Remove from library |
| GET | `/user-books/search?search={query}` | Search books in communities |

### Shares (Lending)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/shares?userbookid={id}` | Request to borrow |
| GET | `/shares/borrower` | Your borrows |
| GET | `/shares/lender` | Your lends |
| GET | `/shares/borrower/archived` | Archived borrows |
| GET | `/shares/lender/archived` | Archived lends |
| PUT | `/shares/{id}/status` | Update share status |
| PUT | `/shares/{id}/return-date` | Set return date |
| POST | `/shares/{id}/archive` | Archive share |
| POST | `/shares/{id}/unarchive` | Unarchive share |

### Chat

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/shares/{shareId}/chat/messages` | Get messages (paginated) |
| POST | `/shares/{shareId}/chat/messages` | Send message |

### Notifications

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/notifications` | Get unread notifications |
| PATCH | `/notifications/shares/{shareId}/read` | Mark share notifications read |
| PATCH | `/notifications/shares/{shareId}/chat/read` | Mark chat notifications read |

### Communities

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/communities` | List all communities |
| GET | `/communities/{id}` | Get community details |
| POST | `/communities?name={name}` | Create community |
| DELETE | `/communities/{id}` | Delete community |
| POST | `/community-users/join/{communityId}` | Join community |
| DELETE | `/community-users/leave/{communityId}` | Leave community |
| GET | `/community-users/user/{userId}` | Get user's communities |
| GET | `/community-users/community/{communityId}` | Get community members |

### SignalR Hub

Connect to `/chathub?access_token={token}` for real-time chat.

**Client methods:** `JoinShareChat`, `LeaveShareChat`, `SendMessage`
**Server events:** `ReceiveMessage`, `JoinedChat`, `LeftChat`, `Error`

### Legal

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/privacy-policy` | Privacy policy page |

> All endpoints except `/auth/*` and `/privacy-policy` require a JWT token: `Authorization: Bearer <token>`

## Share Workflow

```
Requested → Ready → PickedUp → Returned → HomeSafe
    ↓         ↓        ↓          ↓
 Declined  Disputed  Disputed   Disputed
```

- **Lender actions:** Ready, HomeSafe, Declined, SetReturnDate
- **Borrower actions:** PickedUp, Returned
- **Either party:** Disputed

## Development

### Build

```bash
dotnet build BookSharingApp.csproj
```

### Run Tests

```bash
# Unit tests (fast, safe to run anytime)
dotnet test BookSharingApp.Tests/BookSharingApp.Tests.csproj

# Verbose output
dotnet test BookSharingApp.Tests/BookSharingApp.Tests.csproj --logger "console;verbosity=detailed"

# Run specific test class
dotnet test BookSharingApp.Tests/BookSharingApp.Tests.csproj --filter "FullyQualifiedName~CreateShareAsync"
```

> **Note:** Integration tests (`BookSharingApp.IntegrationTests/`) hit real external services and cost money. Only run them when changing integration-tested code paths.

### Logging

Logs are emitted as newline-delimited JSON to stdout, structured for ingestion by the Alloy → Loki → Grafana monitoring stack.

Every log entry includes a `RequestId` (from `HttpContext.TraceIdentifier`) that links all service-level log entries for a single request. HTTP method, path, status code, and duration are logged per request via the built-in `HttpLogging` middleware.

Log levels by environment:

| Logger | Development | Production |
|--------|------------|------------|
| Default | `Debug` | `Information` |
| `Microsoft.AspNetCore` | `Information` | `Warning` |
| `Microsoft.AspNetCore.HttpLogging` | `Information` | `Information` |

Example log entry:
```json
{"Timestamp":"2026-03-28T14:22:01.123Z","LogLevel":"Information","Category":"Services.ShareService","Message":"Created share 42 for userbook 7 by borrower user-003","RequestId":"0HN...","State":{"ShareId":42,"UserBookId":7,"BorrowerId":"user-003"}}
```

### Swagger

Swagger UI is available in development mode at `/swagger`.

### Database Seeding

Development mode includes seed data:
- 5 test users (`user-001` through `user-005`, password: `password`)
- 2 communities with members
- 20+ books across various genres
- Sample shares in different workflow states

## Project Structure

```
BookSharingWebAPI/
├── Common/         # Shared constants and enums
├── Data/           # EF Core DbContext and seeding
├── Endpoints/      # Minimal API endpoint definitions
├── Hubs/           # SignalR hubs (ChatHub)
├── Middleware/      # Custom middleware (rate limiting)
├── Migrations/     # EF Core migrations
├── Models/         # Entity models and DTOs
├── Services/       # Business logic layer
├── Validators/     # Business rule validators
├── wwwroot/images/ # Book cover thumbnails
├── BookSharingApp.Cli/       # Admin CLI tool (report viewer)
└── BookSharingApp.Tests/     # Unit tests (xUnit)
```

## Admin CLI

A command-line tool for viewing and managing chat message reports. Admin access is gated by server/SSH access — no additional auth or API endpoints required.

### Commands

| Command | Description |
|---------|-------------|
| `reports list` | List all reports (newest first) |
| `reports list --user <name>` | Filter by reported user name |
| `reports view <id>` | Full report detail with share context |
| `reports stats` | Total count, breakdown by category, top reported users |

### Usage (Docker)

The CLI is included in the Docker image. Start an interactive session with `docker compose exec`, then run commands at the prompt. The connection string is picked up automatically from the container environment.

```bash
docker compose exec -it booksharing-api admin
```

```
BookSharing Admin CLI
Type 'help' for available commands, 'exit' to quit.

>> reports list
>> reports list --user "john"
>> reports view 42
>> reports stats
>> exit
```

One-shot mode is also supported by passing commands directly:

```bash
docker compose exec booksharing-api admin reports stats
```

### Usage (Local)

```bash
# Interactive mode
dotnet run --project BookSharingApp.Cli -- --connection-string "Host=localhost;Port=5432;Database=booksharingdb;Username=bookuser;Password=YOUR_PASSWORD"

# One-shot mode
dotnet run --project BookSharingApp.Cli -- --connection-string "Host=localhost;..." reports list
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `POSTGRES_DB` | Database name |
| `POSTGRES_USER` | Database username |
| `POSTGRES_PASSWORD` | Database password |
| `DB_CONNECTION_STRING` | Full connection string |
| `JWT_KEY` | JWT signing key (32+ characters) |

## License

All rights reserved.
