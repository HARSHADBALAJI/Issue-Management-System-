# Phase 6: Bug Report
Generated: 2026-06-23
Status: All verified bugs fixed and re-tested

---

## CLASSIFICATION

| Priority | Count |
|---|---|
| Critical | 0 (all fixed) |
| High | 1 |
| Medium | 2 |
| Low | 2 |

---

## HIGH PRIORITY

### H-1: Frontend has zero API integration

- **File**: `frontend/src/` (all files)
- **Type**: Architecture / Missing Implementation
- **Status**: Unresolved
- **Description**: The frontend application is a fully self-contained mock/demo that operates entirely on in-memory data. It makes zero HTTP calls to the backend. All CRUD operations modify JavaScript arrays that reset on page refresh. There is no authentication flow, no API service layer, no loading states, and no error handling.
- **Impact**: The application cannot function with the backend without a complete integration effort.
- **Severity**: High
- **Required work**:
  1. Create `src/services/api.js` with `fetch`/`axios` wrapper
  2. Implement JWT login flow with token storage and Bearer headers
  3. Replace all mock data imports with API calls
  4. Add loading, empty, and error states to all components
  5. Map field names between API (PascalCase) and frontend (camelCase)
  6. Convert bool fields (`isActive`) to string fields (`"active"`/`"inactive"`)
  7. Format integer IDs with string prefixes (`USR-`, `TICK-`, etc.)

---

## MEDIUM PRIORITY

### M-1: Application entity has no alias field

- **File**: `backend/TicketSystem.Api/Models/Entities/Application.cs` and DTOs
- **Type**: Missing Feature
- **Status**: Unresolved
- **Description**: The `Application` entity and DTOs lack an `Alias` field. The frontend mock data includes `alias` (e.g., `"SiteMon"`), and the ticket detail response computes an `ApplicationAlias` from the application name (truncated, uppercased). A proper alias field is needed.
- **Impact**: Frontend cannot display/configure application aliases. The computed alias differs from user expectations.
- **Fix**: Add `Alias` column to `Applications` table, update entity, DTOs, and remove computed alias from ticket detail.

### M-2: Department entity has no head/manager field

- **File**: `backend/TicketSystem.Api/Models/Entities/Department.cs`
- **Type**: Missing Feature
- **Status**: Unresolved
- **Description**: The `Department` entity has no `HeadUserId` or `HeadName` field. The frontend mock data includes `head: "Sarah Johnson"` for each department.
- **Impact**: Department management in frontend cannot show/assign department heads.
- **Fix**: Add `HeadUserId` (nullable FK to Users) to Department entity and DTOs.

---

## LOW PRIORITY

### L-1: Hardcoded connection string in Program.cs

- **File**: `backend/TicketSystem.Api/Program.cs`
- **Type**: Configuration
- **Status**: Unresolved
- **Description**: The SQL Server connection string is hardcoded in Program.cs:
  ```
  "Server=localhost\\SQLEXPRESS;Database=ticketing_system;Trusted_Connection=True;TrustServerCertificate=True;"
  ```
- **Impact**: Cannot deploy to different environments without code change.
- **Fix**: Move to `appsettings.json` or environment variables.

### L-2: UserId not extracted from JWTs in most controllers

- **File**: `backend/TicketSystem.Api/Controllers/UsersController.cs`, `ApplicationsController.cs`, `DepartmentsController.cs`
- **Type**: Enhancement
- **Status**: Fixed in TicketsController only; others still missing
- **Description**: The `TicketsController` now extracts `userId` from JWT claims for message/status/CA creation. Other controllers (Users, Applications, Departments) do not extract the current user.
- **Impact**: Audit logging cannot track who performed mutations.
- **Fix**: Add `GetUserId()` helper to base controller or each controller similarly.

---

## FIXED BUGS (Resolved in this session)

### F-1 (Critical): GetNextSequenceAsync returns bigint but code uses int

- **File**: `TicketRepository.cs:190`
- **Fix**: Changed `SqlQueryRaw<int>` to `SqlQueryRaw<long>` with cast to `(int)`.
- **Symptom**: `InvalidCastException` on ticket creation.

### F-2 (Critical): AgentPerformance query uses unsupported EF translation

- **File**: `DashboardService.cs`
- **Fix**: Added `.ToListAsync()` before LINQ operations involving `DefaultIfEmpty`/`Average` on nullable DateTime spans.
- **Symptom**: `ArgumentException` on `GET /api/dashboard/agent-performance`.

### F-3 (Critical): Tickets have no ApplicationName in create response

- **File**: `TicketService.cs:56-59`
- **Fix**: Added `IApplicationRepository` dependency to look up application name/alias after create.
- **Symptom**: Create response had empty application name/alias.

### F-4 (Critical): Route conflicts on ticket sub-routes

- **File**: `TicketsController.cs`
- **Fix**: Added `{id:int}` route constraints to prevent `stats`, `sla-summary`, `bulk-assign` from matching as `id`.
- **Symptom**: `GET /api/tickets/stats` returned 404 (matched as id parameter).

### F-5 (Critical): DbInitializer doesn't seed Requesters

- **File**: `DbInitializer.cs`
- **Fix**: Added requester seeding (John Requester, Jane Requester).
- **Symptom**: `KeyNotFoundException: Requester not found` on ticket creation.

### F-6 (Critical): AddMessageAsync doesn't persist ticket messages

- **File**: `TicketService.cs:81-109`
- **Fix**: Added `_ticketRepo.AddMessageAsync(message)` to persist message entity. Also added `UserId` and corrected `MessageSourceType` to satisfy `CK_TicketMessage_SourceType` check constraint.
- **Symptom**: Messages returned `Id=0` and were never saved to database.

### F-7 (Critical): UpdateStatusAsync doesn't create TicketStatusHistory records

- **File**: `TicketService.cs:149-162`
- **Fix**: Added creation of `TicketStatusHistory` entity with `FromStatusId`, `ToStatusId`, `ChangedByUserId`, `Remarks` and saved via `AddStatusHistoryAsync`.
- **Symptom**: Status history was empty in ticket detail response.

### F-8 (Critical): AddCorrectiveActionAsync doesn't persist corrective actions

- **File**: `TicketService.cs:111-134`
- **Fix**: Added `_ticketRepo.AddCorrectiveActionAsync(action)` to persist the entity. Also added `PerformedByUserId` from JWT.
- **Symptom**: Corrective actions were never saved to database.
