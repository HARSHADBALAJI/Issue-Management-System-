# Phase 5: Frontend ↔ Backend Compatibility Matrix
Generated: 2026-06-23

## Overview

The frontend is a mock-only React application with NO API integration. All data is hardcoded in `src/data/` files. Connecting to the live API requires:
1. Creating an API service layer (`src/services/api.js`)
2. Transforming API responses to match frontend expectations
3. Implementing JWT auth flow
4. Adding loading/error states

---

## 1. TICKET ENDPOINT COMPATIBILITY

### GET /api/tickets (list) + GET /api/tickets/{id} (detail)

| Frontend Field | Frontend Expects | API Returns | Match? | Action Needed |
|---|---|---|---|---|
| `id` | `"TICK-1001"` (string) | `id: 1` (int) + `ticketNumber: "TKT-1001"` | MISMATCH | Use `ticketNumber` as `id` |
| `application` | `"CRM Platform"` (name string) | `applicationName: "..."` | MATCH | Rename field |
| `department` | `"Engineering"` | `assignedToDepartment` (detail only, nullable) | MISSING | Add to list endpoint or compute client-side from user |
| `raisedBy` | `"Sarah Johnson"` | `requesterName` | MATCH | Rename field |
| `assignedTo` | `"Alice Cooper"` | `assignedToName` (nullable) | MATCH | Rename field |
| `subject` | `"Unable to export..."` | `subject` | MATCH | Direct mapping |
| `description` | `(long text)` | `description` | MATCH | Direct mapping |
| `status` | `"in_progress"` | `statusName` | MATCH | Direct mapping |
| `sla` | `"12h 30m remaining"` | `slaDeadline` (DateTime) + `isSlaBreached` (bool) | MISMATCH | Compute display string client-side |
| `updated` | `Date` | `updatedAt` (DateTime) | MATCH | Direct mapping |
| `priority` | (derived from id/index) | `priority: "high"` (string) | MATCH | Add to frontend ticket model |
| `createdAt` | (not used) | `createdAt` | ADD | Available but unused |

### POST /api/tickets (create)

| Frontend Sends | API Expects | Match? |
|---|---|---|
| Frontend doesn't create tickets via API | `requesterId`, `applicationId`, `subject`, `description`, `priority` | New implementation needed |

### PUT /api/tickets/{id}/status (status change)

| Frontend Action | API Expects | Match? |
|---|---|---|
| `onStatusChange(ticketId, newStatus)` | `PUT /api/tickets/{id}/status` with `{ statusId: int, remarks: string }` | Requires mapping status string → statusId |

### PUT /api/tickets/{id}/assign (assignment)

| Frontend Action | API Expects | Match? |
|---|---|---|
| `onAssign(ticketId, userId)` | `PUT /api/tickets/{id}/assign` with `{ assignedToUserId: int }` | Requires mapping user id |

### POST /api/tickets/{id}/messages (reply)

| Frontend Action | API Expects | Match? |
|---|---|---|
| Reply in TicketDetail form | `POST /api/tickets/{id}/messages` with `{ content, isInternal }` | New implementation needed |

### POST /api/tickets/bulk-assign, /bulk-status (bulk)

| Frontend Action | API Expects | Match? |
|---|---|---|
| BulkActions component | Bulk assign/status endpoints | New implementation needed |

---

## 2. USER ENDPOINT COMPATIBILITY

| Frontend Field | Frontend Expects | API Returns | Match? | Action Needed |
|---|---|---|---|---|
| `id` | `"USR-1001"` (string) | `id: 1` (int) | MISMATCH | Use string `"USR-$id"` or add employeeId prefix |
| `name` | `"Sarah Johnson"` | `fullName` | MATCH | Rename field |
| `email` | `"sarah@company.com"` | `email` | MATCH | Direct mapping |
| `department` | `"Engineering"` (name) | `departmentName` | MATCH | Rename field |
| `role` | `"spoc"` / `"admin"` | `roleName` (lowercase) | MATCH | Direct mapping |
| `status` | `"active"` / `"inactive"` | `isActive` (bool) | MISMATCH | Convert bool → string |
| `assignedApps` | `["CRM Platform", ...]` | `applications` (list with `name`) | MATCH | Extract names from response |
| `createdDate` | `Date` | `createdAt` | MATCH | Rename field |
| `lastLogin` | `Date` | `lastLoginAt` | MATCH | Rename field |

---

## 3. APPLICATION ENDPOINT COMPATIBILITY

| Frontend Field | Frontend Expects | API Returns | Match? | Action Needed |
|---|---|---|---|---|
| `id` | `"APP-001"` (string) | `id: 1` (int) | MISMATCH | Format as `"APP-{N:03d}"` |
| `name` | `"Site Monitoring"` | `name` | MATCH | Direct mapping |
| `description` | `"Real-time..."` | `description` | MATCH | Direct mapping |
| `alias` | `"SiteMon"` | Not in API | MISSING | API has no alias field |
| `supportEmail` | `"sitemon@company.com"` | `supportEmail` | MATCH | Direct mapping |
| `status` | `"active"` / `"inactive"` | `isActive` (bool) | MISMATCH | Convert bool → string |
| `assignedUserIds` | `["USR-1003", ...]` | Not in list endpoint | MISSING | Need separate call or include in response |
| `createdDate` | `Date` | `createdAt` | MATCH | Rename field |
| `lastUpdated` | `Date` | `updatedAt` | MATCH | Rename field |

---

## 4. DEPARTMENT ENDPOINT COMPATIBILITY

| Frontend Field | Frontend Expects | API Returns | Match? | Action Needed |
|---|---|---|---|---|
| `id` | `"DEPT-001"` (string) | `id: 1` (int) | MISMATCH | Format as `"DEPT-{N:03d}"` |
| `name` | `"Engineering"` | `name` | MATCH | Direct mapping |
| `head` | `"Sarah Johnson"` | Not in API | MISSING | API has no department head field |
| `userCount` | `5` | Not in API | MISSING | Compute client-side from users |
| `spocCount` | `3` | Not in API | MISSING | Compute client-side from users |
| `adminCount` | `2` | Not in API | MISSING | Compute client-side from users |

---

## 5. DASHBOARD ENDPOINT COMPATIBILITY

| Frontend Need | API Endpoint | Match? | Notes |
|---|---|---|---|
| KPI counts (total, in_progress, waiting, resolved, closed) | `GET /api/tickets/stats` | MATCH | Returns all needed counts |
| SLA breaches | `GET /api/tickets/stats` has `slaBreached` | MATCH | Direct mapping |
| Avg resolution time | `GET /api/tickets/stats` has `avgResolutionTime` | MATCH | API returns formatted string like "4.5h" |
| SLA compliance % | `GET /api/tickets/stats` has `slaCompliance` | MATCH | Direct mapping |
| Priority distribution | `GET /api/tickets/stats` has `priorityDistribution` | MATCH | Returns array of `{ priority, count }` |
| Trends chart data | `GET /api/dashboard/trends?days=N` | MATCH | Returns daily created/resolved/breached counts |
| Agent performance | `GET /api/dashboard/agent-performance` | MATCH | Returns agent-level stats |
| SLA summary per ticket | `GET /api/tickets/sla-summary` | ADD | New data source for SLA panel |

---

## 6. AUTH ENDPOINT COMPATIBILITY

| Frontend Need | API Endpoint | Match? | Notes |
|---|---|---|---|
| Login form | `POST /api/auth/login` | NEW | Returns `accessToken`, `refreshToken`, `user` |
| Token refresh | `POST /api/auth/refresh` | NEW | Returns new tokens |
| Logout | `POST /api/auth/logout` | NEW | Invalidates refresh token |
| Change password | `POST /api/auth/change-password` | NEW | Not yet implemented in frontend |

---

## 7. NOTIFICATION ENDPOINT COMPATIBILITY

| Frontend Need | API Endpoint | Match? | Notes |
|---|---|---|---|
| Unread notifications | `GET /api/notifications?unreadOnly=true` | NEW | Not yet implemented in frontend |
| Mark as read | `PUT /api/notifications/{id}/read` | NEW | |
| Mark all read | `PUT /api/notifications/read-all` | NEW | |

---

## 8. SUMMARY OF MISMATCHES BY SEVERITY

### Critical (blocks integration)
1. **No API service layer** — frontend has zero HTTP calls
2. **ID format** — string IDs with prefixes vs integer IDs; needs consistent mapping
3. **No auth flow** — login, token storage, bearer header, token refresh not implemented

### High (requires transformation)
1. **Field naming** — camelCase vs PascalCase; needs renaming (e.g. `assignedTo` vs `assignedToName`)
2. **Type conversion** — `isActive` (bool) vs `"active"/"inactive"` (string); `slaDeadline` (DateTime) vs `"12h 30m remaining"` (display string)
3. **Department head** — API doesn't have department head field
4. **Application alias** — API doesn't have alias field; `ApplicationAlias` on ticket response is computed differently

### Medium (missing data)
1. **User `assignedApps`** — not returned in user list API
2. **Application `assignedUserIds`** — not in list response
3. **Dashboard filtering** — frontend filters locally; needs to pass filter params to API
4. **Pagination** — frontend paginates mock data locally; needs integration with API paging
