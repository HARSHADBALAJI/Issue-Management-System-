# Manual Testing Checklist — Ticket System

## Prerequisites

**Start the backend** (terminal 1):
```powershell
cd backend/TicketSystem.Api
dotnet run
```
The backend serves the API at `http://localhost:5001` and also serves the pre-built frontend at the same URL.
> If you prefer the Vite dev server (hot reload), start it in a **second** terminal:
> ```powershell
> cd frontend
> npm run dev
> ```
> Then open `http://localhost:5173` instead.

**Test credentials:**
| Role | Email | Password |
|------|-------|----------|
| Admin | admin@ticketingsystem.com | Admin@123 |
| SPOC | uday@Intecc.com | (ask admin to set) |

---

## 1. Login

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 1.1 | Open `http://localhost:5001` (or `http://localhost:5173`) | — | — | — | Login page appears with email + password fields |
| 1.2 | Enter `admin@ticketingsystem.com` / `Admin@123` and click Login | `POST /api/auth/login` | — | — | Redirected to Dashboard; sidebar visible |
| 1.3 | Verify `accessToken` and `refreshToken` in `localStorage` (F12 > Application > Local Storage) | — | — | — | Both tokens present, not expired |
| 1.4 | Refresh the page | `GET /api/auth/refresh` on token expiry | — | — | Dashboard reloads without re-prompting login |

**Edge case:** Enter wrong password → `401 Unauthorized` → error message "Invalid credentials" shown, no redirect.

---

## 2. Dashboard Counts

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 2.1 | On Dashboard, view KPI cards | `GET /api/tickets` (with stats) | — | — | Cards show: Total, Open, In Progress, Waiting, Resolved, Closed counts matching DB |
| 2.2 | Click date range filter | `GET /api/tickets?from=...&to=...` | — | — | Charts and table refresh with filtered data |
| 2.3 | Switch application filter dropdown | `GET /api/tickets?applicationId=...` | — | — | Only tickets for that app shown |

---

## 3. Ticket Creation (Manual via API — no UI button exists)

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 3.1 | Open browser DevTools console | — | — | — | — |
| 3.2 | Run the following to create a ticket: `fetch('http://localhost:5001/api/tickets', { method: 'POST', headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + localStorage.getItem('accessToken') }, body: JSON.stringify({ subject: 'Test manual ticket', description: 'Created from UI test', applicationId: 84, priority: 'medium', requesterEmail: 'test@example.com', requesterName: 'Test User' }) })` | `POST /api/tickets` | `Tickets`, `Requesters`, `TicketStatusHistory`, `TicketMessages` | New ticket with StatusId=1, next sequence number | Ticket appears in the Tickets table via SignalR real-time update (no refresh needed) |
| 3.3 | Navigate to Tickets page | `GET /api/tickets?page=1&pageSize=20` | — | — | New ticket visible at the top of the table with "In Progress" status |

---

## 4. Ticket Details

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 4.1 | On Tickets page, click the **Ticket ID** link of any ticket | `GET /api/tickets/{id}` | — | — | Full ticket detail page opens |
| 4.2 | Verify info in the summary bar | — | — | — | Shows: Ticket Number, Status, Application, SPOC, Priority, SLA |
| 4.3 | Scroll through conversation timeline | — | — | — | All messages, status changes, and corrective actions shown in chronological order |
| 4.4 | Refresh the page | `GET /api/tickets/{id}` | — | — | Same detail page reloads correctly |

---

## 5. Send Reply

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 5.1 | On Ticket Detail page, type a message in the reply composer | — | — | — | Textarea fills with content |
| 5.2 | Click **Send** | `POST /api/tickets/{id}/messages` | `TicketMessages`, `Tickets.UpdatedAt` | New message row with UserId=1, IsInternal=false | Message appears in conversation timeline immediately (SignalR) |
| 5.3 | Toggle **Internal** checkbox and send again | `POST /api/tickets/{id}/messages` | `TicketMessages` | New message row with IsInternal=true | Message appears with "Internal" badge, only visible to SPOC/Admin |

**Edge case:** Send empty message → validation error, not sent.
**Edge case:** Send very long message (5000+ chars) → message saved, displayed without truncation.

---

## 6. Change Status

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 6.1 | On Ticket Detail page, open **Status** dropdown | — | — | — | Dropdown shows available statuses |
| 6.2 | Select **Waiting for Requester** and add a note | `PUT /api/tickets/{id}/status` | `Tickets.StatusId`, `TicketStatusHistory` | StatusId changes from 1 to 2; new history row | Status badge updates; timeline shows "Changed to Waiting" |
| 6.3 | Select **Resolved** | `PUT /api/tickets/{id}/status` | `Tickets.StatusId`, `Tickets.ResolvedAt`, `TicketStatusHistory` | StatusId=3; ResolvedAt set | Status shows "Resolved" with timestamp |
| 6.4 | Select **Closed** | `PUT /api/tickets/{id}/status` | `Tickets.StatusId`, `Tickets.ClosedAt`, `TicketStatusHistory` | StatusId=4; ClosedAt set | Status shows "Closed" with timestamp |
| 6.5 | Click **Reopen** button | `POST /api/tickets/{id}/reopen` | `Tickets.StatusId`, `Tickets.ResolvedAt=NULL`, `Tickets.ClosedAt=NULL`, `TicketStatusHistory` | StatusId=1; ResolvedAt/ClosedAt cleared | Status reverts to "In Progress" |

**Edge case:** Try to reopen a ticket that's already In Progress → `400 Bad Request` — error message.
**Edge case:** Change status twice quickly → both changes recorded in history timeline.

---

## 7. Assign SPOC

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 7.1 | On Ticket Detail page, use **Assign SPOC** search field | `GET /api/users?search=...` | — | — | Dropdown shows matching users |
| 7.2 | Select a user (e.g. "Uday") | `PUT /api/tickets/{id}/assign` | `Tickets.AssignedToUserId` | AssignedToUserId set to 2 | SPOC name updates in summary bar |
| 7.3 | Verify notification is created | `INSERT INTO Notifications` | `Notifications` | New row with UserId=2, Type="ticket_assigned" | Uday sees notification bell indicator when they log in |

**Edge case:** Re-assign to different user → old SPOC is replaced; notification sent to new SPOC.

---

## 8. Corrective Action

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 8.1 | On Ticket Detail page, expand **Corrective Actions** section | — | — | — | Textarea and Add button visible |
| 8.2 | Type a description and click **Add** | `POST /api/tickets/{id}/corrective-actions` | `TicketCorrectiveActions` | New row with description, UserId=1 | Action appears in the list below |
| 8.3 | Add another corrective action | `POST /api/tickets/{id}/corrective-actions` | `TicketCorrectiveActions` | Second row | Both actions listed chronologically |

---

## 9. Search

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 9.1 | On Tickets page, type in **Search** field | `GET /api/tickets?search=zoho` | — | — | Table filters to show only tickets matching "zoho" in subject/description |
| 9.2 | Clear the search | `GET /api/tickets` | — | — | All tickets shown again |

**Edge case:** Search with special characters (`!@#$%`) → handled gracefully (no error).
**Edge case:** Search for a ticket number (`TKT-1079`) → exact match shown.

---

## 10. Filters

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 10.1 | Click a **Status tab** (e.g. "Open") | `GET /api/tickets?status=open` | — | — | Only tickets with status "open" shown; active tab highlighted |
| 10.2 | Select an **Application** filter | `GET /api/tickets?applicationId=84` | — | — | Only Zoho application tickets shown |
| 10.3 | Pick a **Date Range** | `GET /api/tickets?from=...&to=...` | — | — | Only tickets within date range shown |
| 10.4 | Combine multiple filters (Status + Application + Date) | `GET /api/tickets?status=in_progress&applicationId=84&from=...&to=...` | — | — | Tickets matching ALL filters shown |

---

## 11. Attachments

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 11.1 | On reply composer, click **Attach** (paperclip icon) | — | — | — | File picker opens |
| 11.2 | Select an image file | `POST /api/tickets/{id}/messages` (multipart) | `TicketMessages`, `TicketAttachments` | Message created + attachment row with FileData | Attachment shown below the message with thumbnail |
| 11.3 | Click the attachment thumbnail | `GET /api/attachments/{id}` | — | — | Image opens in browser or downloads |

**Edge case:** Attach a 20MB+ file → rejected with "File too large" error.
**Edge case:** Attach a `.exe` or `.dll` → rejected for security reasons.
**Edge case:** Attach a PDF → displays as downloadable link (no preview).

---

## 12. Email Processing (End-to-End)

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 12.1 | From your personal email, send an email to `kwlnnethra9@gmail.com` with Subject **"Zoho test from email"** and body **"Testing the zoho application mapping from email"** | — | — | — | — |
| 12.2 | Wait **up to 15 seconds** (poll interval) | Backend's `EmailReceiverService` polls IMAP | — | — | — |
| 12.3 | Backend receives email | `EmailReceiverService` calls `EmailProcessingService.ProcessEmailAsync` | — | — | — |
| 12.4 | AI extracts application | `POST` to Azure OpenAI GPT-4o | — | — | AI returns: Application="Zoho", Confidence=0.5+ |
| 12.5 | Application resolution | `EmailProcessingService.ResolveApplicationAsync` compares against `Applications` table | — | — | "Zoho" matches DB record "zoho" (case-insensitive) → uses application ID 84 |
| 12.6 | Ticket created | `INSERT INTO Tickets` | `Tickets`, `Requesters`, `TicketMessages`, `EmailMessages`, `TicketStatusHistory` | New ticket with ApplicationId=84, AssignedToUserId=9 (Harshad), StatusId=1 | Ticket appears in the frontend Tickets table via SignalR (no refresh needed) |
| 12.7 | Open the new ticket | — | — | — | Application shows **"zoho"**, SPOC shows **"Harshad"**, subject is "Zoho test from email" |
| 12.8 | Check email notification | `EmailService` sends `NewTicketRaised.html` | `EmailMessages` | Row with Direction="Outgoing", Recipient=Harshad's email | Harshad receives "New Ticket: TKT-10XX" email |

**Edge case — Unknown application:** Send email with subject "Random gibberish xyz123" → AI returns Application="Unknown" or low confidence → ticket created with Application="Unknown", no SPOC assigned.

**Edge case — Empty body:** Send email with subject "Test" and empty body → ticket created with subject "Test" and description="" (not placeholder text).

**Edge case — Reply to existing ticket:** Reply to the email notification you received → email receiver detects `In-Reply-To` header → message appended to existing ticket conversation.

---

## 13. Notifications

| Step | Action | Expected API Call | DB Tables Changed | Expected DB Changes | Expected UI Result |
|------|--------|-------------------|-------------------|---------------------|--------------------|
| 13.1 | Login as SPOC (e.g., `uday@Intecc.com`) | — | — | — | Dashboard loads |
| 13.2 | Click the **bell icon** in the top nav | `GET /api/notifications?unreadOnly=true` | — | — | Dropdown shows unread notifications |
| 13.3 | Click a notification that references a ticket | — | — | — | Navigates to that ticket's detail page |
| 13.4 | Verify notification is now marked as read | `PUT /api/notifications/{id}/read` | `Notifications.IsRead` | IsRead changes from 0 to 1 | Bell badge count decreases |

**Edge case:** Mark all as read → `PUT /api/notifications/read-all` → all notifications for the user set to IsRead=true.

---

## 14. Ticket Lifecycle (Full Flow)

| Step | Action | Expected Status |
|------|--------|----------------|
| 14.1 | Create ticket (via email or API) | **In Progress** |
| 14.2 | SPOC replies | Stays **In Progress** |
| 14.3 | Change to **Waiting for Requester** | **Waiting** |
| 14.4 | Requester replies (via email reply) | **In Progress** (auto-transitioned back) |
| 14.5 | Change to **Resolved** | **Resolved** |
| 14.6 | Change to **Closed** | **Closed** |
| 14.7 | Click **Reopen** | **In Progress** |
| 14.8 | Auto-close (after 3 days of Resolved with no replies) | **Closed** (by background service) |

---

## 15. Edge Cases

| # | Scenario | Expected Behavior |
|---|----------|-------------------|
| 15.1 | **Duplicate email**: Send same email twice | Second one detected by `MessageId` duplicate check → skipped, not re-created |
| 15.2 | **Forwarded email**: Forward a support email to the inbox | Processed as a new ticket (no In-Reply-To matching) |
| 15.3 | **HTML-only email**: Send email with no plaintext version | AI extracts visible text from HTML |
| 15.4 | **Multiple applications mentioned**: Email mentions "Zoho" and "SAP" | AI returns one; if multiple DB matches → forced to "Unknown" for manual triage |
| 15.5 | **AI service failure**: Azure OpenAI is unreachable | Fallback: uses raw email body as description, Application="Unknown" |
| 15.6 | **Empty subject email**: Send email without subject | Ticket created with subject="" |
| 15.7 | **Very long email (100K chars)** | Ticket created with full body as description (no truncation) |
| 15.8 | **Email with only an image attachment** | AI processes image; extracts info if readable |
| 15.9 | **Attachments with corrupted data** | Skipped individually; ticket created with remaining attachments |
| 15.10 | **Two admins editing same ticket simultaneously** | Last write wins; no data corruption |
| 15.11 | **Apply status filter + search + date range together** | Results match ALL criteria |
| 15.12 | **Expired JWT token** | Auto-refresh via `refreshToken`; seamless to user |
| 15.13 | **Network disconnect mid-operation** | API returns error; UI shows error toast |

---

## Issues Found During Verification

1. **Backend port mismatch** — `apiClient.js` uses `http://localhost:5001/api` but `launchSettings.json` uses `http://localhost:5001` for HTTP ✓ (correct, no issue)
2. **No UI "Create Ticket" button** — Tickets can only enter the system via email or direct API calls
3. **TKT-1079 already modified** — During notification testing, TKT-1079 was assigned to Uday and cycled through all statuses. It's currently in "In Progress" (reopened). This does not affect new testing.

---

*Generated on 2026-06-26*
