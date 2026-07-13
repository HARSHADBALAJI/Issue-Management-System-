# MOCK DATA AUDIT — Final Report
Date: 2026-06-23
Verdict: **MOCK DATA REMAINING = YES**

---

## PHASE 1: MOCK DATA INVENTORY

### File: `src/data/tickets.js`
| Line(s) | Mock Data | Description |
|---------|-----------|-------------|
| 1-4 | `APPS` array | 8 hardcoded application names |
| 6-9 | `NAMES` array | 10 hardcoded requester names |
| 11 | `DEPARTMENTS` array | 8 hardcoded department names |
| 13-16 | `SPOCS` array | 8 hardcoded agent names |
| 18-39 | `SUBJECTS` array | 20 hardcoded ticket subjects |
| 41-62 | `DESCRIPTIONS` array | 20 hardcoded ticket descriptions |
| 64 | `STATUSES` array | 4 status strings |
| 66-83 | `ri()`, `rd()`, `rs()` | Random data generators for SLA, dates |
| 87-100 | 28 ticket objects generated | All fields are fabricated |

### File: `src/data/users.js`
| Line(s) | Mock Data | Description |
|---------|-----------|-------------|
| 1-5 | `APPS` array | 10 hardcoded app names (duplicate from tickets.js) |
| 7-10 | `DEPARTMENTS` array | 8 hardcoded dept names (duplicate from tickets.js) |
| 12-22 | `FIRST_NAMES` / `LAST_NAMES` arrays | 20 first names + 20 last names |
| 36-58 | 24 user objects generated | All fields are fabricated |

### File: `src/data/departments.js`
| Line(s) | Mock Data | Description |
|---------|-----------|-------------|
| 1-10 | 8 department objects | id, name, head, userCount, spocCount, adminCount are all hardcoded |

### File: `src/data/applications.js`
| Line(s) | Mock Data | Description |
|---------|-----------|-------------|
| 1-112 | 10 application objects | id, name, description, alias, supportEmail, status, assignedUserIds, dates all hardcoded/fabricated |

### File: `src/components/TicketDetail.jsx`
| Line(s) | Mock Data | Description |
|---------|-----------|-------------|
| 3-8 | `COMMENT_AUTHORS` | 4 fabricated author objects |
| 10-21 | `COMMENT_BODIES` | 10 fabricated comment texts |
| 23-28 | `INTERNAL_BODIES` | 5 fabricated internal note texts |
| 31-36 | `SYS_EVENTS_DATA` | 4 fabricated system events |
| 38-44 | `ATTACHMENT_FILES` | 5 fabricated file attachments |
| 46-52 | `TABLE_DATA` | 4 rows of fabricated table data |
| 53 | `CC_RECIPIENTS` | 2 fabricated email addresses |
| 55-67 | `hashId`, `pick`, `pickSlice` | Random generators for pseudo-random timeline |
| 95-137 | Timeline computation | Creates fake conversation timeline from mock data, sorted by fabricated timestamps |
| 139-141 | `conversationMessages` | Filters timeline to exclude system events |
| 143-151 | `handleSend`, `handleClose` | `alert()` calls — no API interaction |
| All SLA sidebar | Hardcoded SLA data | "SLA Breached — OVERDUE", "49h 54m consumed", "Level 3 — Project Manager" |

### File: `src/components/Dashboard.jsx`
| Line(s) | Mock Data | Description |
|---------|-----------|-------------|
| 14-28 | `PRIORITY_COLORS`, `PRIORITY_LABELS` | Priority config constants |
| 30-37 | `KPI_CONFIG` | KPI card definitions |
| 39 | `FILTER_PRESETS` | Date filter presets |
| 41-45 | `assignPriority()` | **Fake priority algorithm** — derives priority from ticket ID character codes, NOT from database |
| 60-80 | `HOUR_LABELS`, `HOUR_DATA` | **Fake hourly chart data** — sine wave formula, not from database |
| 152-159 | `filterOptions` | Derives filter lists from mock tickets |
| 207-216 | `filteredTickets` | Filters mock tickets array |
| 232-243 | `statusCounts` | Counts from mock data |
| 245-257 | `priorityDist` | Uses fake `assignPriority()` algorithm |
| 259-272 | `slaCompliance` | Parses fake SLA strings with regex |
| 285-306 | `agentStats` | Computed from mock tickets |
| 309-385 | `trendChartData` | Uses HOUR_DATA + fallback sine wave for days with no data |

### File: `src/components/TicketTable.jsx`
| Line(s) | Mock Data | Description |
|---------|-----------|-------------|
| 25-27 | Pagination | Operates on mock `tickets` array |
| 30-43 | `handleAction` | **Mutates mock data directly** — cycles status via `ticket.status = opts[...]`, not API |
| 45-55 | `columns` | Column definitions |

### File: `src/components/UsersPage.jsx`
| Line(s) | Mock Data | Description |
|---------|-----------|-------------|
| 3 | `import { ALL_APPS, DEPARTMENTS } from '../data/users'` | Imports mock APP/DEPARTMENT arrays |
| 43-46 | `handleEditSave` | Mutates mock user object in memory |
| 49-63 | `handleAssignSave` | Mutates `detailUser.assignedApps` in memory |
| 65-68 | `handleResetPassword` | Generates fake password via `Math.random()` |
| 70-75 | `handleDeleteUser` | Filters mock `users` array |
| 81-96 | `handleCreateUser` | Creates user with fabricated `USR-2001+` id |
| 99-110 | `handleExport` | Exports from mock data |

### File: `src/components/DepartmentsPage.jsx`
| Line(s) | Mock Data | Description |
|---------|-----------|-------------|
| 2 | `import DEPARTMENTS_DATA from '../data/departments'` | Imports all 8 hardcoded departments |
| 7 | `useState(DEPARTMENTS_DATA)` | Initializes state from mock data |
| 23-33 | `handleAdd` | Creates department with fake `DEPT-{N}` id |
| 36-39 | `handleEdit` | Mutates local `list` state |
| 42-45 | `handleDelete` | Filters local `list` state |

### File: `src/components/ApplicationsPage.jsx`
| Line(s) | Mock Data | Description |
|---------|-----------|-------------|
| All CRUD | All mutations | Operates on mock `applications` prop array |
| 75-91 | `handleCreateApp` | Creates app with fabricated `APP-{N}` id, computed alias |
| 42-57 | `handleSaveMapping` | Mutates `app.assignedUserIds` in memory |
| 59-62 | `handleToggleStatus` | Toggles `app.status` string in memory |
| 99-105 | `handleEditSave` | `Object.assign()` on mock data |

### File: `src/App.jsx`
| Line(s) | Mock Data | Description |
|---------|-----------|-------------|
| 15-18 | 4 mock data imports | Imports TICKETS, USERS, ALL_APPS, APPLICATION_DATA |
| 22-28 | State initialized from mock data | `useState(USERS)`, `useState(APPLICATION_DATA)`, `useState(TICKETS)` |
| 33-36 | `addNotification` | Creates local notification object |
| 43-50 | `ticket:update` event listener | Re-renders from `[...TICKETS]` mock array |
| 52-87 | `filtered` | Filters mock `tickets` array locally |
| 122-133 | `handleBulkStatus` | **Mutates mock tickets** — cycles status in memory |
| 135-146 | `handleBulkAssign` | Uses `prompt()` to assign — no API |
| 148-163 | `handleBulkExport` | Exports from mock data |
| 186-237 | All page rendering | Passes mock data to all components |

---

## PHASE 2: API USAGE AUDIT

| Page | Current Data Source | Mock? | API Connected? | Backend Endpoint |
|------|-------------------|-------|---------------|-----------------|
| Dashboard | `src/data/tickets.js` + `assignPriority()` algorithm | YES | NO | `GET /api/dashboard/stats`, `/trends`, `/sla`, `/agent-performance` |
| Tickets (List) | `src/data/tickets.js` | YES | NO | `GET /api/tickets` |
| Ticket Detail | `src/data/tickets.js` + `TicketDetail.jsx` hardcoded arrays | YES | NO | `GET /api/tickets/{id}` |
| Ticket Messages | `COMMENT_AUTHORS`/`COMMENT_BODIES` in `TicketDetail.jsx` | YES | NO | `GET /api/tickets/{id}` (includes messages) |
| Users | `src/data/users.js` | YES | NO | `GET /api/users` |
| Departments | `src/data/departments.js` | YES | NO | `GET /api/departments` |
| Applications | `src/data/applications.js` | YES | NO | `GET /api/applications` |
| User Detail | `src/data/users.js` (in-memory object) | YES | NO | `GET /api/users/{id}` |
| Department CRUD | Local state from `DEPARTMENTS_DATA` | YES | NO | `POST/PUT/DELETE /api/departments` |
| Application CRUD | `applications` prop (from `src/data/applications.js`) | YES | NO | `POST/PUT/DELETE /api/applications` |
| User CRUD | `users` prop (from `src/data/users.js`) | YES | NO | `POST/PUT/DELETE /api/users` |

---

## PHASE 3: FRONTEND TO API MAPPING (Field Gaps)

| Component | Frontend Field | API Field Available? | Notes |
|-----------|---------------|-------------------|-------|
| Departments Page | `head` | ❌ No | API has no DepartmentHead |
| Departments Page | `userCount` | ❌ No | Must compute from DB |
| Departments Page | `spocCount` | ❌ No | Must compute from DB |
| Departments Page | `adminCount` | ❌ No | Must compute from DB |
| Applications Page | `alias` | ❌ No | API has no alias field |
| Applications Page | `assignedUserIds` as strings | ❌ No | API uses int IDs |
| Applications Page | `assignedUserIds` in list | ❌ No | Not in list response |
| Applications Page | `createdDate`/`lastUpdated` as Date | ⚠️ Partial | API returns strings/ISO |
| Ticket Table | `sla` as display string | ❌ No | API returns `slaDeadline` DateTime |
| Ticket Detail | Conversation timeline | ❌ No | No endpoint for full conversation |
| Ticket Detail | Attachments | ❌ No | No attachment upload/download |
| Ticket Detail | CC recipients | ❌ No | No email integration yet |
| Ticket Detail | SLA panel with consumed/paused | ❌ No | API only has deadline + breached |
| Dashboard | Priority distribution | ❌ No | Must use API `priorityDistribution` |
| Dashboard | Chart data points | ❌ No | Must use API `GET /api/dashboard/trends` |
| Dashboard | Agent performance | ❌ No | Must use API `GET /api/dashboard/agent-performance` |
| Auth | Login/logout | ❌ No | JWT flow not implemented |
| Notifications | Unread count | ❌ No | `GET /api/notifications` not connected |

---

## PHASE 4–7: AT A GLANCE

| Phase | Status | Evidence Required |
|-------|--------|------------------|
| 4. Dashboard Validation | ❌ FAIL | Dashboard uses `assignPriority()` algorithm + fake SLA strings + sine wave chart data |
| 5. Ticket Detail Validation | ❌ FAIL | Conversation timeline is fabricated from `COMMENT_AUTHORS`/`COMMENT_BODIES` arrays |
| 6. Network Verification | ❌ FAIL | Zero HTTP calls exist anywhere in the frontend |
| 7. Database Validation | ❌ FAIL | No database connection; changing data in SQL Server would have zero effect |

---

## SUMMARY

**MOCK DATA REMAINING = YES**

### Mock files to delete:
- `src/data/tickets.js` (102 lines)
- `src/data/users.js` (61 lines)
- `src/data/departments.js` (12 lines)
- `src/data/applications.js` (114 lines)

### Components with hardcoded arrays to replace:
- `src/components/TicketDetail.jsx` — COMMENT_AUTHORS, COMMENT_BODIES, INTERNAL_BODIES, SYS_EVENTS_DATA, ATTACHMENT_FILES, TABLE_DATA, CC_RECIPIENTS
- `src/components/Dashboard.jsx` — HOUR_DATA, assignPriority(), fake priority distribution

### Estimated effort: **3-4 days** for a senior frontend developer to:
1. Create API service layer with JWT auth flow
2. Replace all 4 mock data files with API calls
3. Implement loading/error/empty states in all 13 components
4. Build proper conversation timeline from API data
5. Connect dashboard to real API endpoints
6. Implement proper SLA display logic
7. Add attachment upload/download UI
8. Add CC/email function stubs

### Recommendation:
Before deleting mock data, you need to build an API service layer. The existing frontend is 100% self-contained mock with zero HTTP calls. Proceeding to email pipeline (MailKit/IMAP/SMTP) is premature until the frontend connects to the existing backend.
