# FRONTEND ↔ BACKEND MAPPING AUDIT

---

## PART 1: SCREEN-BY-SCREEN DOCUMENT

---

### 1. DEPARTMENTS PAGE

**Purpose**: Manage support departments, view department-level user counts.

#### UI Components
- Page heading with "Add Department" button
- KPI row (3 cards)
- Search input
- Data table (6 columns)
- Row action dropdown (3 dots)
- Add/Edit modal (`DeptFormModal`)
- Pagination (10/page)

#### Displayed Fields

| UI Field | Display Logic | Source |
|----------|--------------|--------|
| Department Name | `d.name` | Departments.Name |
| Department ID | `d.id` shown muted below name | Departments.Id |
| Department Head | `d.head` or `—` | Users.Name (dept head) |
| Total Users | `d.userCount` | COUNT(Users) per department |
| SPOCs | `d.spocCount` | COUNT(Users) WHERE Role=SPOC per dept |
| Admins | `d.adminCount` | COUNT(Users) WHERE Role=Admin per dept |

#### KPI Cards

| Card | Calculation |
|------|------------|
| Total Departments | `departments.length` |
| Total Users Across Departments | SUM(d.userCount) |
| Departments with SPOCs | COUNT(d WHERE d.spocCount > 0) |

#### Filters
- Search: case-insensitive substring on Department Name

#### Sorting
- None in frontend (backend should default sort by name)

#### Pagination
- 10 per page, prev/next + numbered buttons

#### CREATE Flow (Add Department)
1. Modal opens with empty `name` and `head` fields
2. User fills `name` (required), `head` (optional)
3. POST /api/departments → generates new ID, default counts = 0
4. Returns created department; frontend appends to list

#### UPDATE Flow (Edit Department)
1. Modal pre-filled with current `name`, `head`
2. User edits fields
3. PUT /api/departments/{id} with `{ name, head }`
4. Returns updated department; frontend replaces in list

#### DELETE Flow
1. Confirmation dialog (`window.confirm`)
2. DELETE /api/departments/{id} → soft delete (IsActive = false)
3. Frontend removes from list

#### Validation Rules
- Name: required, max 100 chars, unique
- Head: optional, must reference existing User.Id if provided
- Cannot delete department with active users

#### Required API Endpoints
| Method | Path | Purpose |
|--------|------|---------|
| GET | /api/departments | List all departments (with computed user/spoc/admin counts) |
| GET | /api/departments/{id} | Single department detail with user counts |
| POST | /api/departments | Create department |
| PUT | /api/departments/{id} | Update department name/head |
| DELETE | /api/departments/{id} | Soft delete department |

#### Required Database Tables
- Departments (Id, Name, Head can be UserId FK or string)
- Users (for computing UserCount, SpocCount, AdminCount)
- Roles (for SpocCount/AdminCount filtering)

**Design Decision**: The frontend stores `head` as a string (user name), but the DB should reference Users.Id for referential integrity. When fetching departments, resolve User.FullName for the head display. All counts should be computed dynamically from the Users table, not stored.

---

### 2. USERS PAGE

**Purpose**: Full CRUD for SPOC/Admin users, manage application assignments.

#### UI Components
- Page heading with "Add User" button
- KPI row (4 cards)
- Filter bar (search + 3 dropdowns + export)
- Data table (6 columns)
- Row action dropdown
- **User Detail Modal**: Avatar + Quick Actions (Edit, Assign Apps, Reset Password, Remove) + User Info (7 fields) + Assigned Applications tags
- **Edit User Modal**: 5 form fields
- **Add User Modal**: 5 form fields (2 required)
- **Assign Applications Modal**: Dual-list transfer with search
- Pagination (10/page)

#### Displayed Fields

| UI Field | Source | Notes |
|----------|--------|-------|
| Name (avatar + name) | Users.FullName | Initials derived from name |
| Email | Users.Email | |
| Department | Departments.Name | FK -> Departments |
| Role | Roles.Name | Badge: "Admin" or "SPOC" |
| Status | Users.IsActive | Badge: "Active" / "Inactive" |
| Created Date | Users.CreatedAt | Formatted: "January 15, 2024" |
| Last Login | Users.LastLogin | Via timeAgo() helper |
| Assigned Apps | UserApplications -> Applications.Name | Tags with remove button |

#### KPI Cards

| Card | Calculation |
|------|------------|
| Total Users | COUNT(Users) |
| SPOCs | COUNT(Users WHERE Role.Name = 'SPOC') |
| Admins | COUNT(Users WHERE Role.Name = 'Admin') |
| Active Users | COUNT(Users WHERE IsActive = true) |

#### Filters
| Filter | Type | Options |
|--------|------|---------|
| Search | Text | Matches FullName or Email (case-insensitive) |
| Role | Dropdown | All, SPOC, Admin |
| Department | Dropdown | All + distinct department names |
| Status | Dropdown | All, Active, Inactive |

#### Sorting
- None in frontend (backend should default sort by name or created date)

#### Pagination
- 10 per page

#### CREATE Flow (Add User)
1. Modal opens: Name* (text), Email* (email), Department (select), Role (select: SPOC/Admin), Status (select: Active/Inactive)
2. POST /api/users with `{ fullName, email, departmentId, roleId, isActive }`
3. Backend generates password (or sends setup email), hashes, stores
4. Returns created user; frontend appends

#### UPDATE Flow (Edit User)
1. Modal pre-filled with current name, email, department, role, status
2. PUT /api/users/{id} with `{ fullName, email, departmentId, roleId, isActive }`
3. Returns updated user; frontend replaces in list

#### DELETE Flow (Remove User)
1. Confirmation dialog
2. DELETE /api/users/{id} → soft delete (IsActive = false)
3. Frontend removes from list

#### Assign Applications Flow
1. GET /api/users/{id}/applications → current assigned apps
2. Dual-list shows available vs assigned
3. PUT /api/users/{id}/applications with `{ applicationIds: [1,2,3] }`
4. Backend computes adds/removes, updates UserApplications
5. Returns updated list; frontend refreshes

#### Reset Password Flow
1. POST /api/users/{id}/reset-password
2. Backend generates temp password, returns it (or sends email)
3. Frontend shows via alert()

#### Validation Rules
- FullName: required, max 100 chars
- Email: required, valid email, unique
- DepartmentId: required, must reference existing Department
- RoleId: required, must be Admin or SPOC
- Password: auto-generated on create (min 8 chars with complexity)
- Cannot soft-delete user who has assigned tickets (set AssignedToUserId = NULL first)

#### Required API Endpoints
| Method | Path | Purpose |
|--------|------|---------|
| GET | /api/users | List users with filters (search, role, department, status) |
| GET | /api/users/{id} | Single user detail with assigned applications |
| POST | /api/users | Create user |
| PUT | /api/users/{id} | Update user |
| DELETE | /api/users/{id} | Soft delete user |
| GET | /api/users/{id}/applications | Get user's assigned application IDs |
| PUT | /api/users/{id}/applications | Update user's assigned applications |
| POST | /api/users/{id}/reset-password | Reset user password |

#### Required Database Tables
- Users, Departments, Roles, Applications, UserApplications

---

### 3. APPLICATIONS PAGE

**Purpose**: Manage applications, track assigned users per application.

#### UI Components
- Page heading with "Add Application" + "Export" buttons
- KPI row (3 cards)
- Filter bar (search + status dropdown)
- Data table (4 columns)
- Row action dropdown (5 items)
- **App Detail Modal**: Tabbed (Details / Manage Users)
- **Add Application Modal**: 2 form fields
- **Edit Application Modal**: 2 form fields
- Pagination (10/page)

#### Displayed Fields

| UI Field | Source | Notes |
|----------|--------|-------|
| Application Name | Applications.Name | With icon (first letter) + alias below |
| Assigned Users | COUNT(UserApplications) | Clickable, opens Manage Users tab |
| Status | Applications.IsActive | Badge: "Active" / "Inactive" |
| Description | Applications.Description | Shown in detail modal |
| Created Date | Applications.CreatedAt | Formatted in detail modal |
| Last Updated | Applications.UpdatedAt | Formatted in detail modal |
| Support Email | Applications.SupportEmail | In mock data, not shown in any current UI |

**Note**: The `alias`, `supportEmail` fields exist in mock data but are NOT displayed in any UI component. The `alias` is auto-generated on create.

#### KPI Cards

| Card | Calculation |
|------|------------|
| Total Applications | COUNT(Applications) |
| Total Assigned Users | SUM(assignedUserIds.length) across all apps |
| Active Applications | COUNT(WHERE IsActive = true) |

#### Filters
| Filter | Type | Options |
|--------|------|---------|
| Search | Text | Matches Name or alias (case-insensitive) |
| Status | Dropdown | All, Active, Inactive |

#### Sorting
- None in frontend (backend default by name)

#### Pagination
- 10 per page

#### CREATE Flow (Add Application)
1. Modal opens: Name* (text, required), Status (select: Active/Inactive)
2. POST /api/applications with `{ name, isActive }`
3. Backend auto-generates alias from name
4. Returns created application; frontend appends

#### UPDATE Flow (Edit Application)
1. Modal pre-filled with name, status
2. PUT /api/applications/{id} with `{ name, isActive }`
3. Returns updated application; frontend replaces in list

#### DELETE Flow
1. Confirmation dialog
2. DELETE /api/applications/{id} → soft delete (IsActive = false)
3. Frontend removes from list

#### Toggle Status Flow
1. PUT /api/applications/{id}/toggle-status
2. Flips IsActive between true/false
3. Frontend updates in list

#### Manage Users (Assign/Unassign) Flow
1. GET /api/applications/{id}/users → list of assigned user IDs
2. Dual-list shows available (all users not assigned) vs assigned
3. PUT /api/applications/{id}/users with `{ userIds: [1,2,3] }`
4. Backend computes adds/removes, triggers notifications
5. Frontend refreshes counts

#### Validation Rules
- Name: required, max 100 chars, unique
- Alias: auto-generated unique code from name (uppercase, no spaces, max 10 chars)
- Cannot delete application with active tickets

#### Required API Endpoints
| Method | Path | Purpose |
|--------|------|---------|
| GET | /api/applications | List applications with filters (search, status) |
| GET | /api/applications/{id} | Single application detail |
| POST | /api/applications | Create application |
| PUT | /api/applications/{id} | Update application |
| DELETE | /api/applications/{id} | Soft delete application |
| PUT | /api/applications/{id}/toggle-status | Toggle active/inactive |
| GET | /api/applications/{id}/users | Get assigned user IDs for an application |
| PUT | /api/applications/{id}/users | Update assigned users for an application |

#### Required Database Tables
- Applications, Users, UserApplications

---

### 4. TICKETS PAGE (Dashboard + List)

**Purpose**: View ticket metrics, filter/search tickets, perform bulk operations.

#### Dashboard Sub-components

**Dashboard.jsx** components:

| Component | Fields |
|-----------|--------|
| 6 KPI Cards | Total Tickets, In Progress, Waiting, Resolved, Closed, SLA Breached |
| SLA Compliance (3 mini-cards) | SLA Breaches count, Avg Resolution Time, SLA Compliance % |
| Priority Donut Chart | Critical, High, Medium, Low, Informational (computed from ticket ID hash) |
| Trends Line Chart | Created, Resolved, SLA Breached, Reopened over time |
| Agent Performance Table | Agent, Assigned, Resolved, Open, SLA %, Avg Resolution |

**KPICards.jsx** (Tickets page):
| Card | Description |
|------|------------|
| Total | All tickets |
| Open | in_progress + waiting + resolved (non-closed) |
| In Progress | status = in_progress |
| Waiting | status = waiting |
| Closed | status = closed |

**StatusTabs.jsx**:
| Tab | Filter |
|-----|--------|
| All | No filter |
| Open | status IN (in_progress, waiting, resolved) |
| In Progress | status = in_progress |
| Waiting | status = waiting |
| Closed | status = closed |

**Filters.jsx**:
| Filter | UI | Behavior |
|--------|----|----------|
| Search | Text input | Matches TicketNumber, Subject, Description, Requester.FullName, User.FullName |
| Status | Dropdown | All, Open, In Progress, Waiting, Resolved, Closed (overrides tab) |
| Application | Dropdown | All + distinct application names |
| From Date | Date picker | Filters by Ticket.UpdatedAt >= from |
| To Date | Date picker | Filters by Ticket.UpdatedAt <= to |
| Export | Button | Exports filtered list as CSV |

**TicketTable.jsx** (tickets list):
| Column | Data Source | Display |
|--------|-------------|---------|
| Checkbox | Selection state | Bulk select |
| Ticket ID | Ticket.TicketNumber | Clickable, opens detail |
| Application | Ticket.ApplicationId -> Application.Name | |
| Subject | Ticket.Subject | |
| Description | Ticket.Description | Expandable box |
| Status | Ticket.StatusId -> TicketStatus.Name | Color-coded pill via getStatusMeta() |
| SLA | Computed | Colored dot via slaDot() |
| Updated | Ticket.UpdatedAt | Via timeAgo() |
| Raised By | Ticket.RequesterId -> Requester.FullName | |
| Assigned To | Ticket.AssignedToUserId -> User.FullName | |

**BulkActions.jsx**:
| Action | Behavior |
|--------|----------|
| Change Status | Cycles selected tickets: in_progress→waiting→resolved→closed |
| Assign SPOC | Prompts for SPOC name, assigns to all selected |
| Export | Exports selected tickets as CSV |

**Row-level action dropdown** (TicketTable):
| Action | Behavior |
|--------|----------|
| View Details | Navigate to ticket detail page |
| Assign SPOC | Prompt for SPOC name |
| Change Status | Cycles status in_progress→waiting→resolved→closed |

#### Dashboard Global Filters
| Filter | Options |
|--------|---------|
| Date Preset | Today, Yesterday, Last 7 Days, Last 30 Days, This Month, Custom Range |
| Application | All + distinct application names |
| Department | All + distinct department strings (from ticket data) |
| User (assignedTo) | All + distinct assigned user names |
| Status | All, in_progress, waiting, resolved, closed |

#### Dashboard Section Filter
| Filter | Options |
|--------|---------|
| Status | All, in_progress, waiting, resolved, closed |
| Priority | All, critical, high, medium, low |

#### Required API Endpoints for Tickets List
| Method | Path | Purpose |
|--------|------|---------|
| GET | /api/tickets | List tickets with filters, pagination, sorting |
| GET | /api/tickets/stats | Dashboard statistics (KPI, SLA, charts) |
| GET | /api/tickets/stats/agent-performance | Agent resolution metrics |
| GET | /api/tickets/stats/trends | Time-series chart data |
| PUT | /api/tickets/{id}/status | Change single ticket status |
| PUT | /api/tickets/bulk/status | Bulk change status |
| PUT | /api/tickets/bulk/assign | Bulk assign SPOC |
| GET | /api/tickets/export | Export filtered tickets as CSV |

#### Required Database Tables
- Tickets, TicketStatuses, Applications, Requesters, Users, Departments, TicketStatusHistory

**Design Decisions**:
- **Priority**: The frontend computes priority from ticket ID hash. In the backend, Priority should be a stored field on Tickets table (already exists: `nvarchar(20)`). The Dashboard priority donut chart should read from this stored field.
- **SLA**: Currently a mock string. In production, SLA is computed: compare `Ticket.CreatedAt` + configured SLA target hours against current time. SLA Breaches = tickets where current time > (CreatedAt + SLA target). Track paused time via waiting status intervals.
- **Department in Tickets**: The frontend stores department as a string per ticket. In the DB, this should reference `Departments.Id` via `Ticket.ApplicationId -> ApplicationRoutingRule -> DepartmentId`, or be added as a direct FK. **Decision**: Add DepartmentId FK to Tickets table, or resolve department from ApplicationRoutingRule. **Preferred**: Add Ticket.DepartmentId as optional FK.
- **Reopened count**: Tracked via TicketStatusHistory where status transitioned TO in_progress FROM resolved/closed.
- **Updated field**: The frontend uses `ticket.updated` for timeAgo display and date filtering. This maps to `Ticket.UpdatedAt`.

#### API for Dashboard KPI
GET /api/tickets/stats should return:
```json
{
  "total": 28,
  "inProgress": 7,
  "waiting": 7,
  "resolved": 7,
  "closed": 7,
  "slaBreached": 4,
  "avgResolutionTime": "2h 15m",
  "slaCompliance": 85.7,
  "priorityDistribution": [
    { "priority": "critical", "count": 2 },
    { "priority": "high", "count": 5 },
    { "priority": "medium", "count": 12 },
    { "priority": "low", "count": 7 },
    { "priority": "informational", "count": 2 }
  ]
}
```

---

### 5. TICKET DETAIL PAGE

**Purpose**: View full ticket thread, respond to requesters, manage status, log corrective actions.

#### UI Components
- Header: breadcrumb (My Tickets > Ticket ID), title, Print/Reply buttons
- Summary Bar: 3 cards (Status, SPOC, Application)
- Composer: Toolbar (attach, mention, bold, italic, lists, link) + textarea + Send Reply
- Conversation Timeline: threaded messages (replies, internal notes, system events)
- Side Panel:
  - Ticket Details section (Status dropdown, SPOC, Application, CC list, Corrective Actions textarea + Save Draft/Submit, Close Ticket)
  - SLA Tracking section (hardcoded mock)

#### Displayed Fields

**Summary Bar**:
| Field | Database Source |
|-------|----------------|
| Status | Ticket.StatusId -> TicketStatus.DisplayName |
| SPOC | Ticket.AssignedToUserId -> User.FullName |
| Application | Ticket.ApplicationId -> Application.Name |

**Side Panel - Ticket Details**:
| Field | Database Source | UI Widget |
|-------|----------------|-----------|
| Status | TicketStatus | Dropdown: Open, In Progress, Waiting, Resolved, Closed |
| SPOC | User.FullName | Read-only span |
| Application | Application.Name | Read-only span |
| CC list | Hardcoded mock | Envelope icon + email |
| Corrective Actions | TicketCorrectiveActions.Description | Textarea + Save Draft/Submit |
| Close Ticket | Ticket.StatusId | Button |

**Side Panel - SLA Tracking** (currently hardcoded mock):
| Field | Production Source |
|-------|------------------|
| Breached banner | Computed from SLA target vs elapsed time |
| Target | Application-level SLA config (hours) |
| Consumed | Elapsed time since CreatedAt minus paused time |
| Paused | Sum of durations in 'waiting' status |
| Progress bar | Consumed / Target * 100 |
| Consumed % | Same as above |
| Escalation | Computed based on breach level |
| Breached timestamp | When SLA was breached |

**Composer**:
| Element | Behavior |
|---------|----------|
| Textarea | Free text, required for send |
| Toolbar buttons | UI only (non-functional) |
| Send Reply button | POST /api/tickets/{id}/messages |

**Conversation Timeline**:
| Message Field | Source |
|--------------|--------|
| Author avatar | User initials or Requester initials |
| Author name | User.FullName or Requester.FullName |
| Author role | "SPOC", "Admin", "Requester", "System" |
| Timestamp | TicketMessage.CreatedAt |
| Message body | TicketMessage.Content |
| Attachments | TicketAttachments (FileName, FileSize, ContentType) |
| System events | TicketStatusHistory entries |
| Embedded table | Mock data only (not in DB) |

#### Status Update Flow
1. User selects new status from dropdown
2. PUT /api/tickets/{id}/status with `{ statusId, remarks? }`
3. Validates transition (see status flow rules)
4. Creates TicketStatusHistory entry
5. Creates system TicketMessage if applicable
6. Sends notifications
7. Returns updated ticket

**Status Transition Rules**:
- in_progress ↔ waiting (with requester reply = auto-switch to waiting)
- in_progress → resolved (by SPOC)
- waiting → resolved (by SPOC)
- resolved → in_progress (auto, when requester replies to resolved ticket)
- resolved → closed (manual by SPOC/Admin)
- closed → in_progress (auto, when requester replies to closed ticket)
- All other transitions: blocked

#### Corrective Actions Flow
1. User types in textarea
2. **Save Draft**: No API call (frontend-only, could use localStorage)
3. **Submit**: POST /api/tickets/{id}/corrective-actions with `{ description, performedAt }`
4. Creates TicketCorrectiveAction record
5. Returns created action; frontend clears textarea

#### Close Ticket Flow
1. User clicks Close Ticket button
2. PUT /api/tickets/{id}/status with `{ statusId: 4, remarks: "Closed by SPOC" }`
3. Sets Ticket.ClosedAt = UTC now
4. Validates: ticket must be in Resolved status first
5. Creates TicketStatusHistory
6. Returns updated ticket

#### Reply/Send Message Flow
1. User types reply, optionally attaches files
2. POST /api/tickets/{id}/messages with `{ content, isInternal, attachments? }`
3. If requester replies: sets MessageSourceType = 'Requester', links to RequesterId
4. If SPOC replies: sets MessageSourceType = 'User', links to UserId
5. If system message: sets MessageSourceType = 'System'
6. If requester replies to resolved/closed ticket: auto-reopens (status→in_progress)
7. Sends email notification to requester/SPOC
8. Returns created message with attachment metadata

#### Required API Endpoints
| Method | Path | Purpose |
|--------|------|---------|
| GET | /api/tickets/{id} | Single ticket with all relations |
| GET | /api/tickets/{id}/messages | Full conversation timeline |
| POST | /api/tickets/{id}/messages | Add reply/message |
| POST | /api/tickets/{id}/attachments | Upload attachment |
| PUT | /api/tickets/{id}/status | Update ticket status |
| PUT | /api/tickets/{id}/assign | Assign/reassign SPOC |
| GET | /api/tickets/{id}/status-history | Status change history |
| POST | /api/tickets/{id}/corrective-actions | Submit corrective action |
| GET | /api/tickets/{id}/sla | SLA tracking data |

#### Required Database Tables
- Tickets, TicketMessages, TicketAttachments, TicketStatusHistory, TicketCorrectiveActions
- Users (SPOC), Requesters, Applications, TicketStatuses
- EmailMessages (tracking), Notifications

---

## PART 2: FRONTEND TO BACKEND MAPPING MATRIX

---

### DEPARTMENTS PAGE MAPPING

| UI Field | Database Column | Table | Notes |
|----------|----------------|-------|-------|
| Department Name | Name | Departments | |
| Department Head | FullName (resolved) | Users | FK: Departments.HeadUserId -> Users.Id |
| Total Users | COUNT(Users.Id) | Users | WHERE DepartmentId = Departments.Id |
| SPOCs | COUNT(Users.Id) | Users | WHERE DepartmentId = D.Id AND RoleId = 2 (SPOC) |
| Admins | COUNT(Users.Id) | Users | WHERE DepartmentId = D.Id AND RoleId = 1 (Admin) |
| Add: Name | Name | Departments | Required, unique |
| Add: Head | HeadUserId | Departments | Optional FK -> Users |
| Edit: Name | Name | Departments | |
| Edit: Head | HeadUserId | Departments | |

### USERS PAGE MAPPING

| UI Field | Database Column | Table | Notes |
|----------|----------------|-------|-------|
| Avatar initials | Computed from FullName | — | First letter of each name part |
| Name | FullName | Users | |
| Email | Email | Users | Unique |
| Department | Name (resolved) | Departments | FK: Users.DepartmentId |
| Role | Name (resolved) | Roles | FK: Users.RoleId |
| Status | IsActive | Users | |
| Created Date | CreatedAt | Users | |
| Last Login | RefreshTokenExpiry or LastLoginAt | Users | New field needed? Add LastLoginAt |
| Assigned Apps | Name (resolved list) | Applications | Via UserApplications join |
| KPI: Total Users | COUNT(*) | Users | |
| KPI: SPOCs | COUNT(*) | Users | Where RoleId = 2 (SPOC) |
| KPI: Admins | COUNT(*) | Users | Where RoleId = 1 (Admin) |
| KPI: Active Users | COUNT(*) | Users | Where IsActive = true |
| Add: Name | FullName | Users | Required |
| Add: Email | Email | Users | Required, unique |
| Add: Department | DepartmentId | Users | FK |
| Add: Role | RoleId | Users | FK |
| Add: Status | IsActive | Users | |
| Edit: Name | FullName | Users | |
| Edit: Email | Email | Users | |
| Edit: Department | DepartmentId | Users | |
| Edit: Role | RoleId | Users | |
| Edit: Status | IsActive | Users | |
| Assigned App IDs | ApplicationId | UserApplications | Junction table |
| App tag remove | DELETE UserApplication | UserApplications | WHERE UserId AND ApplicationId |

### APPLICATIONS PAGE MAPPING

| UI Field | Database Column | Table | Notes |
|----------|----------------|-------|-------|
| Application Name | Name | Applications | |
| Alias | Alias (computed) | Applications | Auto-generated, unique |
| Assigned Users count | COUNT(UserApplications) | UserApplications | WHERE ApplicationId |
| Status | IsActive | Applications | |
| Description | Description | Applications | |
| Created Date | CreatedAt | Applications | |
| Last Updated | UpdatedAt | Applications | |
| Support Email | SupportEmail | Applications | In mock data, not in UI |
| KPI: Total Applications | COUNT(*) | Applications | |
| KPI: Total Assigned Users | SUM assigned UserApplications | UserApplications | |
| KPI: Active Applications | COUNT(*) WHERE IsActive=true | Applications | |
| Add: Name | Name | Applications | Required |
| Add: Status | IsActive | Applications | |
| Edit: Name | Name | Applications | |
| Edit: Status | IsActive | Applications | |
| Assigned User IDs | UserId | UserApplications | Junction table |

### TICKETS PAGE MAPPING

| UI Field | Database Column | Table | Notes |
|----------|----------------|-------|-------|
| Ticket ID | TicketNumber | Tickets | Format: TKT-{seq} |
| Application | Name (resolved) | Applications | FK: Tickets.ApplicationId |
| Subject | Subject | Tickets | |
| Description | Description | Tickets | |
| Status | Name (resolved) | TicketStatuses | FK: Tickets.StatusId |
| SLA | Computed | — | CreatedAt vs target, minus paused |
| Updated | UpdatedAt | Tickets | |
| Raised By | FullName (resolved) | Requesters | FK: Tickets.RequesterId |
| Assigned To | FullName (resolved) | Users | FK: Tickets.AssignedToUserId |
| Priority | Priority | Tickets | Stored: critical/high/medium/low/informational |
| Department | Name (resolved) | Departments | FK needed or via ApplicationRoutingRule |

### TICKET DETAIL PAGE MAPPING

| UI Field | Database Column | Table | Notes |
|----------|----------------|-------|-------|
| Summary: Status | DisplayName | TicketStatuses | |
| Summary: SPOC | FullName | Users | |
| Summary: Application | Name | Applications | |
| Side: Status dropdown | Name | TicketStatuses | |
| Side: SPOC | FullName | Users | |
| Side: Application | Name | Applications | |
| Side: CC list | Email | — | Comma-separated on Ticket? Or lookup |
| Side: Corrective Actions | Description | TicketCorrectiveActions | |
| Composer textarea | Content | TicketMessages | |
| Attachments | FileName, FileSize, FileData | TicketAttachments | |
| Timeline: author | FullName | Users or Requesters | |
| Timeline: body | Content | TicketMessages | |
| Timeline: timestamp | CreatedAt | TicketMessages | |
| System events | CreatedAt, remarks | TicketStatusHistory | |

---

## PART 3: API CONTRACT DOCUMENT

---

### AUTH ENDPOINTS

#### POST /api/auth/login
**Request**:
```json
{
  "email": "string",
  "password": "string"
}
```
**Response 200**:
```json
{
  "accessToken": "string",
  "refreshToken": "string",
  "expiresAt": "datetime",
  "user": {
    "id": 1,
    "fullName": "string",
    "email": "string",
    "role": "string",
    "departmentId": 1,
    "departmentName": "string"
  }
}
```

#### POST /api/auth/refresh
**Request**:
```json
{
  "refreshToken": "string"
}
```
**Response 200**: Same as login.

#### POST /api/auth/change-password
**Request**:
```json
{
  "currentPassword": "string",
  "newPassword": "string"
}
```
**Response 200**: `{ "message": "Password changed successfully" }`

---

### DEPARTMENTS ENDPOINTS

#### GET /api/departments
**Query**: `?search=term`
**Response 200**:
```json
[
  {
    "id": 1,
    "name": "Engineering",
    "headUserId": 1,
    "headName": "Sarah Johnson",
    "userCount": 5,
    "spocCount": 3,
    "adminCount": 2,
    "isActive": true
  }
]
```

#### GET /api/departments/{id}
**Response 200**: Single department object (same shape as above).

#### POST /api/departments
**Request**:
```json
{
  "name": "Engineering",
  "headUserId": 1
}
```
**Response 201**: Created department object.

#### PUT /api/departments/{id}
**Request**:
```json
{
  "name": "Engineering",
  "headUserId": 1
}
```
**Response 200**: Updated department object.

#### DELETE /api/departments/{id}
**Response 204**: No content (soft delete: IsActive = false).

---

### USERS ENDPOINTS

#### GET /api/users
**Query**: `?search=term&roleId=1&departmentId=2&isActive=true&page=1&pageSize=10&sortBy=fullName&sortDir=asc`
**Response 200**:
```json
{
  "items": [
    {
      "id": 1,
      "employeeId": "EMP001",
      "fullName": "Sarah Johnson",
      "email": "sarah.johnson@company.com",
      "phoneNumber": null,
      "departmentId": 1,
      "departmentName": "Engineering",
      "roleId": 1,
      "roleName": "Admin",
      "isActive": true,
      "assignedApps": [
        { "applicationId": 1, "applicationName": "CRM Platform" }
      ],
      "createdAt": "2024-01-15T00:00:00Z",
      "lastLoginAt": "2025-06-22T10:30:00Z"
    }
  ],
  "totalCount": 24,
  "page": 1,
  "pageSize": 10,
  "totalPages": 3
}
```

#### GET /api/users/{id}
**Response 200**: Single user object with full details (same shape + `assignedApps` array).

#### POST /api/users
**Request**:
```json
{
  "employeeId": "EMP025",
  "fullName": "Jane Smith",
  "email": "jane.smith@company.com",
  "phoneNumber": null,
  "departmentId": 1,
  "roleId": 2,
  "isActive": true,
  "password": "AutoGeneratedOrSetupToken123"
}
```
**Response 201**: Created user object.

#### PUT /api/users/{id}
**Request**:
```json
{
  "fullName": "Jane Smith",
  "email": "jane.smith@company.com",
  "phoneNumber": null,
  "departmentId": 1,
  "roleId": 2,
  "isActive": true
}
```
**Response 200**: Updated user object.

#### DELETE /api/users/{id}
**Response 204**: Soft delete.

#### GET /api/users/{id}/applications
**Response 200**:
```json
{
  "userId": 1,
  "applicationIds": [1, 3, 5, 7]
}
```

#### PUT /api/users/{id}/applications
**Request**:
```json
{
  "applicationIds": [1, 3, 5, 7, 9]
}
```
**Response 200**: `{ "userId": 1, "applicationIds": [1, 3, 5, 7, 9] }`

#### POST /api/users/{id}/reset-password
**Response 200**:
```json
{
  "temporaryPassword": "Temp@123456"
}
```

---

### APPLICATIONS ENDPOINTS

#### GET /api/applications
**Query**: `?search=term&isActive=true&page=1&pageSize=10&sortBy=name&sortDir=asc`
**Response 200**:
```json
{
  "items": [
    {
      "id": 1,
      "name": "CRM Platform",
      "description": "Customer relationship management platform...",
      "alias": "CRM",
      "supportEmail": "crm@company.com",
      "isActive": true,
      "assignedUserCount": 5,
      "createdAt": "2024-06-15T00:00:00Z",
      "updatedAt": "2025-05-25T00:00:00Z"
    }
  ],
  "totalCount": 10,
  "page": 1,
  "pageSize": 10,
  "totalPages": 1
}
```

#### GET /api/applications/{id}
**Response 200**:
```json
{
  "id": 1,
  "name": "CRM Platform",
  "description": "...",
  "alias": "CRM",
  "supportEmail": "crm@company.com",
  "isActive": true,
  "assignedUserCount": 5,
  "createdAt": "2024-06-15T00:00:00Z",
  "updatedAt": "2025-05-25T00:00:00Z",
  "assignedUsers": [
    { "id": 1, "fullName": "Sarah Johnson", "roleName": "Admin" },
    { "id": 3, "fullName": "Emily Davis", "roleName": "SPOC" }
  ]
}
```

#### POST /api/applications
**Request**:
```json
{
  "name": "New App",
  "description": "Optional description",
  "supportEmail": "newapp@company.com",
  "isActive": true
}
```
**Response 201**: Created application object (alias auto-generated).

#### PUT /api/applications/{id}
**Request**:
```json
{
  "name": "New App Updated",
  "description": "Updated description",
  "supportEmail": "newapp@company.com",
  "isActive": true
}
```
**Response 200**: Updated application object.

#### DELETE /api/applications/{id}
**Response 204**: Soft delete.

#### PUT /api/applications/{id}/toggle-status
**Response 200**: Updated application with flipped `isActive`.

#### GET /api/applications/{id}/users
**Response 200**:
```json
{
  "applicationId": 1,
  "userIds": [1, 3, 5, 7]
}
```

#### PUT /api/applications/{id}/users
**Request**:
```json
{
  "userIds": [1, 3, 5, 7, 9]
}
```
**Response 200**: `{ "applicationId": 1, "userIds": [1, 3, 5, 7, 9] }`

---

### TICKETS ENDPOINTS

#### GET /api/tickets
**Query**: `?search=term&statusId=1&applicationId=2&assignedToUserId=3&fromDate=2025-01-01&toDate=2025-06-23&page=1&pageSize=10&sortBy=createdAt&sortDir=desc`
**Response 200**:
```json
{
  "items": [
    {
      "id": 1,
      "ticketNumber": "TKT-1001",
      "requesterId": 1,
      "requesterName": "Sarah Johnson",
      "applicationId": 1,
      "applicationName": "CRM Platform",
      "assignedToUserId": 3,
      "assignedToName": "Alice Cooper",
      "statusId": 1,
      "statusName": "in_progress",
      "statusDisplayName": "In Progress",
      "subject": "Unable to export customer reports",
      "description": "The user reported that...",
      "priority": "high",
      "isSlaBreached": false,
      "slaConsumed": "2h 30m",
      "slaRemaining": "1h 30m",
      "createdAt": "2025-06-22T10:00:00Z",
      "updatedAt": "2025-06-23T08:30:00Z"
    }
  ],
  "totalCount": 28,
  "page": 1,
  "pageSize": 10,
  "totalPages": 3
}
```

#### GET /api/tickets/stats
**Query**: `?fromDate=...&toDate=...&applicationId=...&departmentId=...&assignedToUserId=...&statusId=...`
**Response 200**:
```json
{
  "total": 28,
  "inProgress": 7,
  "waiting": 7,
  "resolved": 7,
  "closed": 7,
  "slaBreached": 4,
  "avgResolutionTime": "2h 15m",
  "slaCompliance": 85.7,
  "priorityDistribution": [
    { "priority": "critical", "count": 2 },
    { "priority": "high", "count": 5 },
    { "priority": "medium", "count": 12 },
    { "priority": "low", "count": 7 },
    { "priority": "informational", "count": 2 }
  ]
}
```

#### GET /api/tickets/stats/agent-performance
**Query**: Same filters as stats.
**Response 200**:
```json
[
  {
    "agentId": 3,
    "agentName": "Alice Cooper",
    "assigned": 12,
    "resolved": 8,
    "open": 4,
    "slaPercentage": 91.7,
    "avgResolutionTime": "3h 20m"
  }
]
```

#### GET /api/tickets/stats/trends
**Query**: Same filters + `?granularity=hourly|daily`
**Response 200**:
```json
{
  "labels": ["2025-06-01", "2025-06-02", ...],
  "series": [
    { "key": "created", "label": "Created", "data": [5, 3, 7, ...] },
    { "key": "resolved", "label": "Resolved", "data": [4, 2, 6, ...] },
    { "key": "slaBreached", "label": "SLA Breached", "data": [1, 0, 2, ...] },
    { "key": "reopened", "label": "Reopened", "data": [0, 1, 0, ...] }
  ]
}
```

#### GET /api/tickets/{id}
**Response 200**:
```json
{
  "id": 1,
  "ticketNumber": "TKT-1001",
  "requesterId": 1,
  "requesterName": "Sarah Johnson",
  "requesterEmail": "sarah@company.com",
  "applicationId": 1,
  "applicationName": "CRM Platform",
  "assignedToUserId": 3,
  "assignedToName": "Alice Cooper",
  "statusId": 1,
  "statusName": "in_progress",
  "statusDisplayName": "In Progress",
  "subject": "Unable to export customer reports",
  "description": "The user reported that...",
  "priority": "high",
  "createdAt": "2025-06-22T10:00:00Z",
  "updatedAt": "2025-06-23T08:30:00Z",
  "resolvedAt": null,
  "closedAt": null,
  "slaTarget": "4h",
  "slaConsumed": "2h 30m",
  "slaPaused": "0m",
  "slaPercentage": 62.5,
  "isSlaBreached": false,
  "escalationLevel": null
}
```

#### GET /api/tickets/{id}/messages
**Response 200**:
```json
[
  {
    "id": 1,
    "ticketId": 1,
    "requesterId": null,
    "requesterName": null,
    "userId": 3,
    "userName": "Alice Cooper",
    "userRole": "SPOC",
    "messageSourceType": "User",
    "content": "We have identified the root cause...",
    "isInternal": false,
    "createdAt": "2025-06-22T12:00:00Z",
    "attachments": [
      {
        "id": 1,
        "fileName": "error_logs.txt",
        "contentType": "text/plain",
        "fileSize": 24576,
        "createdAt": "2025-06-22T12:00:00Z"
      }
    ]
  },
  {
    "id": 2,
    "ticketId": 1,
    "requesterId": 1,
    "requesterName": "Sarah Johnson",
    "userId": null,
    "userName": null,
    "messageSourceType": "Requester",
    "content": "Thank you for the update...",
    "isInternal": false,
    "createdAt": "2025-06-22T14:00:00Z",
    "attachments": []
  }
]
```

#### POST /api/tickets/{id}/messages
**Request** (multipart/form-data or JSON):
```json
{
  "content": "This is my reply...",
  "isInternal": false
}
```
**Response 201**: Created message object.

#### POST /api/tickets/{id}/attachments
**Request**: multipart/form-data with file(s).
**Response 201**:
```json
{
  "attachmentIds": [1, 2],
  "fileNames": ["error_logs.txt", "screenshot.png"],
  "fileSizes": [24576, 1258291]
}
```

#### PUT /api/tickets/{id}/status
**Request**:
```json
{
  "statusId": 3,
  "remarks": "Issue resolved after deploying the fix"
}
```
**Response 200**:
```json
{
  "id": 1,
  "statusId": 3,
  "statusName": "resolved",
  "statusDisplayName": "Resolved",
  "resolvedAt": "2025-06-23T09:00:00Z",
  "updatedAt": "2025-06-23T09:00:00Z"
}
```

#### PUT /api/tickets/{id}/assign
**Request**:
```json
{
  "assignedToUserId": 5
}
```
**Response 200**: Updated ticket object.

#### GET /api/tickets/{id}/status-history
**Response 200**:
```json
[
  {
    "id": 1,
    "fromStatusId": null,
    "fromStatusName": null,
    "toStatusId": 1,
    "toStatusName": "in_progress",
    "changedByUserId": 1,
    "changedByName": "System",
    "remarks": "Ticket created",
    "createdAt": "2025-06-22T10:00:00Z"
  },
  {
    "id": 2,
    "fromStatusId": 1,
    "fromStatusName": "in_progress",
    "toStatusId": 2,
    "toStatusName": "waiting",
    "changedByUserId": 3,
    "changedByName": "Alice Cooper",
    "remarks": "Waiting for requester information",
    "createdAt": "2025-06-22T15:00:00Z"
  }
]
```

#### POST /api/tickets/{id}/corrective-actions
**Request**:
```json
{
  "description": "Applied database connection pool fix, restarted service",
  "performedAt": "2025-06-23T09:00:00Z"
}
```
**Response 201**:
```json
{
  "id": 1,
  "ticketId": 1,
  "description": "...",
  "performedByUserId": 3,
  "performedByName": "Alice Cooper",
  "performedAt": "2025-06-23T09:00:00Z",
  "createdAt": "2025-06-23T09:00:00Z"
}
```

#### GET /api/tickets/{id}/sla
**Response 200**:
```json
{
  "target": "4h",
  "consumed": "2h 30m",
  "paused": "0m",
  "percentage": 62.5,
  "isSlaBreached": false,
  "escalationLevel": null,
  "breachedAt": null
}
```

#### PUT /api/tickets/bulk/status
**Request**:
```json
{
  "ticketIds": [1, 2, 3],
  "statusId": 2
}
```
**Response 200**:
```json
{
  "updatedCount": 3,
  "tickets": [ ... ]
}
```

#### PUT /api/tickets/bulk/assign
**Request**:
```json
{
  "ticketIds": [1, 2, 3],
  "assignedToUserId": 5
}
```
**Response 200**:
```json
{
  "updatedCount": 3,
  "tickets": [ ... ]
}
```

#### GET /api/tickets/export
**Query**: Same filters as GET /api/tickets.
**Response 200**: CSV file download (`Content-Type: text/csv`).

---

### NOTIFICATIONS ENDPOINTS

#### GET /api/notifications
**Query**: `?isRead=false&page=1&pageSize=20`
**Response 200**:
```json
{
  "items": [
    {
      "id": 1,
      "userId": 3,
      "ticketId": 1,
      "ticketNumber": "TKT-1001",
      "type": "requester_reply",
      "title": "New reply on TKT-1001",
      "message": "Sarah Johnson replied to your ticket",
      "isRead": false,
      "createdAt": "2025-06-23T08:00:00Z"
    }
  ],
  "totalCount": 5,
  "unreadCount": 3
}
```

#### PUT /api/notifications/{id}/read
**Response 200**: `{ "id": 1, "isRead": true }`

#### PUT /api/notifications/read-all
**Response 200**: `{ "updatedCount": 5 }`

---

### REQUESTER LOOKUP ENDPOINT

#### GET /api/requesters
**Query**: `?search=term&email=someone@company.com`
**Response 200**:
```json
[
  {
    "id": 1,
    "fullName": "Sarah Johnson",
    "email": "sarah.johnson@company.com",
    "phoneNumber": null,
    "company": null
  }
]
```
**Note**: Used when creating a ticket to find or auto-create a requester by email.

---

### APPLICATION ROUTING ENDPOINT (for auto-assignment)

#### GET /api/applications/{id}/routing
**Response 200**:
```json
{
  "applicationId": 1,
  "applicationName": "CRM Platform",
  "departmentId": 1,
  "departmentName": "Engineering",
  "primarySpocUserId": 3,
  "primarySpocName": "Alice Cooper",
  "backupSpocUserId": 5,
  "backupSpocName": "Bob Martinez"
}
```

---

### AUDIT LOG ENDPOINT

#### GET /api/audit-logs
**Query**: `?entityType=Ticket&entityId=1&fromDate=...&toDate=...&page=1&pageSize=50`
**Response 200**:
```json
{
  "items": [
    {
      "id": 1,
      "userId": 1,
      "userName": "Admin User",
      "action": "TicketCreated",
      "entityType": "Ticket",
      "entityId": 1,
      "oldValues": null,
      "newValues": "{...}",
      "ipAddress": "192.168.1.100",
      "createdAt": "2025-06-22T10:00:00Z"
    }
  ],
  "totalCount": 50,
  "page": 1,
  "pageSize": 50
}
```

---

### USERS LOOKUP (for dropdowns)

#### GET /api/users/lookup
**Query**: `?roleId=2&departmentId=1`
**Response 200**:
```json
[
  { "id": 3, "fullName": "Alice Cooper", "departmentName": "Engineering" },
  { "id": 5, "fullName": "Bob Martinez", "departmentName": "Engineering" }
]
```
**Purpose**: Populate SPOC dropdowns, department assignment dropdowns, assignee selection.

---

### APPLICATIONS LOOKUP (for dropdowns)

#### GET /api/applications/lookup
**Response 200**:
```json
[
  { "id": 1, "name": "CRM Platform" },
  { "id": 2, "name": "ERP System" }
]
```
**Purpose**: Populate application filter dropdowns in ticket list and dashboard.

---

## ENDPOINT SUMMARY

| # | Method | Path | Screen |
|---|--------|------|--------|
| 1 | POST | /api/auth/login | All |
| 2 | POST | /api/auth/refresh | All |
| 3 | POST | /api/auth/change-password | TopNav |
| 4 | GET | /api/departments | Departments |
| 5 | GET | /api/departments/{id} | Departments |
| 6 | POST | /api/departments | Departments |
| 7 | PUT | /api/departments/{id} | Departments |
| 8 | DELETE | /api/departments/{id} | Departments |
| 9 | GET | /api/users | Users |
| 10 | GET | /api/users/{id} | Users |
| 11 | POST | /api/users | Users |
| 12 | PUT | /api/users/{id} | Users |
| 13 | DELETE | /api/users/{id} | Users |
| 14 | GET | /api/users/{id}/applications | Users |
| 15 | PUT | /api/users/{id}/applications | Users |
| 16 | POST | /api/users/{id}/reset-password | Users |
| 17 | GET | /api/users/lookup | Tickets, Users |
| 18 | GET | /api/applications | Applications |
| 19 | GET | /api/applications/{id} | Applications |
| 20 | POST | /api/applications | Applications |
| 21 | PUT | /api/applications/{id} | Applications |
| 22 | DELETE | /api/applications/{id} | Applications |
| 23 | PUT | /api/applications/{id}/toggle-status | Applications |
| 24 | GET | /api/applications/{id}/users | Applications |
| 25 | PUT | /api/applications/{id}/users | Applications |
| 26 | GET | /api/applications/{id}/routing | Tickets (auto-assign) |
| 27 | GET | /api/applications/lookup | Tickets, Dashboard |
| 28 | GET | /api/tickets | Tickets List |
| 29 | GET | /api/tickets/stats | Dashboard |
| 30 | GET | /api/tickets/stats/agent-performance | Dashboard |
| 31 | GET | /api/tickets/stats/trends | Dashboard |
| 32 | GET | /api/tickets/{id} | Ticket Detail |
| 33 | GET | /api/tickets/{id}/messages | Ticket Detail |
| 34 | POST | /api/tickets/{id}/messages | Ticket Detail |
| 35 | POST | /api/tickets/{id}/attachments | Ticket Detail |
| 36 | PUT | /api/tickets/{id}/status | Ticket Detail |
| 37 | PUT | /api/tickets/{id}/assign | Ticket Detail |
| 38 | GET | /api/tickets/{id}/status-history | Ticket Detail |
| 39 | POST | /api/tickets/{id}/corrective-actions | Ticket Detail |
| 40 | GET | /api/tickets/{id}/sla | Ticket Detail |
| 41 | PUT | /api/tickets/bulk/status | Tickets List |
| 42 | PUT | /api/tickets/bulk/assign | Tickets List |
| 43 | GET | /api/tickets/export | Tickets List |
| 44 | GET | /api/requesters | Tickets (create flow) |
| 45 | GET | /api/notifications | TopNav |
| 46 | PUT | /api/notifications/{id}/read | TopNav |
| 47 | PUT | /api/notifications/read-all | TopNav |
| 48 | GET | /api/audit-logs | Admin |

---

## DESIGN DECISIONS & NOTES

1. **SLA is computed, not stored**: SLA data is calculated on-the-fly from Ticket.CreatedAt + application SLA target minus paused time (time spent in 'waiting' status).

2. **Priority is stored**: The frontend computes priority from ticket ID hash, but the DB should store it. The Tickets table already has a Priority column (`nvarchar(20)`). Frontend priority assignment logic should be replicated on the backend for initial ticket creation, or the frontend should send priority explicitly.

3. **Department in Tickets**: The frontend mock data includes `ticket.department` as a string. In the real schema, this can be derived from ApplicationRoutingRule (which maps Application -> Department), or stored directly as `Ticket.DepartmentId`. Decision: Add `DepartmentId` FK to Tickets for direct access.

4. **User count caching**: DepartmentsPage currently uses hardcoded `userCount`, `spocCount`, `adminCount`. In production these should be computed via COUNT queries on the Users table. The API endpoints for departments already compute these dynamically.

5. **Password management**: On user creation, the backend should generate a secure random password, hash it with BCrypt, and return it (or email it). The frontend shows it via alert().

6. **File upload**: Attachments are sent as multipart/form-data, stored as VARBINARY(MAX) in TicketAttachments.FileData. Limits: 10 MB per file, 25 MB per message.

7. **Notification types**: ticket_assigned, status_change, requester_reply, sla_breach, corrective_action_submitted

8. **CSV Export**: All export endpoints return `Content-Type: text/csv` with proper headers. The frontend's `exportToCSV` helper handles parsing.

9. **Soft Delete pattern**: All IsActive entities (Departments, Roles, Users, Applications, TicketStatuses) use IsActive for soft delete. Tickets and messages are NEVER deleted.

10. **RefreshToken handling**: The frontend doesn't have login UI yet (it's a pure UI prototype). When auth is added, the backend issues JWT (15 min) + RefreshToken (7 days).
