# VAPT Report — Ticketin System

**Assessment Type:** Static Application Security Testing (SAST)  
**Target:** Ticketin System — .NET 8 Backend (Web API + SignalR) + React Frontend  
**Date:** 2026-06-26  
**Severity Scale:** CRITICAL → HIGH → MEDIUM → LOW → INFO  
**Methodology:** OWASP Top 10 (2021), OWASP ASVS, CWE  

---

## Executive Summary

22 security findings were identified across the codebase. The most critical issues include **hardcoded Azure OpenAI API keys and email credentials in a committed `.env` file**, a **weak / hardcoded JWT signing key** enabling token forgery, and **complete absence of role-based authorization** — any authenticated user can perform admin-level operations (create users, reset passwords, manage departments). Input validation is absent across all API endpoints, and JWT tokens are stored in browser `localStorage` (XSS-vulnerable). Immediate remediation is required before any production deployment.

---

## Finding Inventory

| # | Severity | Title | OWASP Top 10 (2021) | File(s) |
|---|----------|-------|---------------------|---------|
| 1 | **CRITICAL** | Hardcoded Secrets in `.env` (Committed to Source) | A01:2021 — Broken Access Control / A05:2021 — Security Misconfiguration | `.env` |
| 2 | **CRITICAL** | Weak / Hardcoded JWT Signing Key | A02:2021 — Cryptographic Failures | `Program.cs`, `AuthService.cs`, `appsettings.json` |
| 3 | **CRITICAL** | Missing Role-Based Authorization | A01:2021 — Broken Access Control | All Controllers |
| 4 | **HIGH** | No Input Validation on Any DTO | A03:2021 — Injection | All `Models/DTOs/` |
| 5 | **HIGH** | JWT Tokens Stored in `localStorage` | A05:2021 — Security Misconfiguration | `AuthContext.jsx`, `apiClient.js` |
| 6 | **HIGH** | No Rate Limiting on Login Endpoint | A04:2021 — Insecure Design | `AuthController.cs` |
| 7 | **HIGH** | User Enumeration Possible | A01:2021 — Broken Access Control | `AuthService.cs` |
| 8 | **HIGH** | Weak / Predictable Default Passwords | A02:2021 — Cryptographic Failures | `DbInitializer.cs`, `UserService.cs` |
| 9 | **HIGH** | SSL/TLS Certificate Validation Disabled | A05:2021 — Security Misconfiguration | `EmailReceiverService.cs` |
| 10 | **HIGH** | Insecure Direct Object Reference (IDOR) on Tickets | A01:2021 — Broken Access Control | `TicketsController.cs` |
| 11 | **MEDIUM** | CORS Overly Permissive | A05:2021 — Security Misconfiguration | `Program.cs` |
| 12 | **MEDIUM** | HTTP (No TLS) in Configuration | A05:2021 — Security Misconfiguration | `launchSettings.json` |
| 13 | **MEDIUM** | Password Change Does Not Verify Current Password | A07:2021 — Identification & Auth Failures | `AuthController.cs` |
| 14 | **MEDIUM** | Refresh Token Rotation Not Enforced | A02:2021 — Cryptographic Failures | `AuthService.cs` |
| 15 | **MEDIUM** | No Account Lockout / Brute-Force Protection | A04:2021 — Insecure Design | `AuthService.cs` |
| 16 | **MEDIUM** | Missing HTTP Security Headers | A05:2021 — Security Misconfiguration | `Program.cs` |
| 17 | **MEDIUM** | SignalR Hub — Missing `[Authorize]` + Group Enumeration | A01:2021 — Broken Access Control | `TicketHub.cs` |
| 18 | **MEDIUM** | Audit Log IP Address Not Captured | A09:2021 — Security Logging & Monitoring Failures | `AuditService.cs` |
| 19 | **LOW** | Large Default Page Size (10,000) | A04:2021 — Insecure Design | `QueryParams.cs` |
| 20 | **LOW** | Known-Vulnerable Package Versions | A06:2021 — Vulnerable Components | `*.csproj`, `package.json` |
| 21 | **INFO** | Hardcoded Admin Credentials in DbInitializer | — | `DbInitializer.cs` |
| 22 | **INFO** | No Email Verification for New Users | — | `UserService.cs` |

---

## Detailed Findings

### CRITICAL

#### Finding 1 — Hardcoded Secrets in `.env` (Committed to Source)

| Field | Value |
|-------|-------|
| **File** | `backend/TicketSystem.Api/.env` |
| **Lines** | 13–14, 18–19, 27–31 |
| **CWE** | CWE-798 (Hardcoded Credentials), CWE-259 (Use of Hardcoded Password) |

**Exposed secrets:**
```
IMAP_PASSWORD=vpivubnwgnmbpgty        # Gmail app password (kwlnnethra9@gmail.com)
SMTP_PASSWORD=vpivubnwgnmbpgty        # Same Gmail app password
API_KEY=  # Azure OpenAI key
AZURE_OPENAI_ENDPOINT=https://lnt-rbf-visionanalytics-foundry.cognitiveservices.azure.com/
```

**Risk:** Any contributor with repository access obtains full control over the Gmail mailbox (IMAP read + SMTP send) and can consume Azure OpenAI API at the organization's cost. The API key is a **real, live credential** — not a placeholder.

**Remediation:**
1. Immediately revoke the Gmail app password and Azure OpenAI API key.
2. Remove the `.env` file from version control (add to `.gitignore`).
3. Use a secrets manager (Azure Key Vault, AWS Secrets Manager, or environment variables on the server).
4. Audit all services for unauthorized access using these leaked credentials.

---

#### Finding 2 — Weak / Hardcoded JWT Signing Key

| Field | Value |
|-------|-------|
| **Files** | `Program.cs:46`, `AuthService.cs:132`, `appsettings.json:10-11` |
| **CWE** | CWE-321 (Use of Hardcoded Cryptographic Key), CWE-330 (Insufficient Entropy) |

Code (three locations):
```csharp
// Program.cs:46 — fallback key
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForTicketSystem2024!@#$%";

// AuthService.cs:132 — same fallback
var jwtKey = _config["Jwt:Key"] ?? "SuperSecretKeyForTicketSystem2024!@#$%";

// appsettings.json:10-11
"Key": "ThisIsADevelopmentSecretKeyThatShouldBeChangedInProduction123!"
```

**Risk:** If `Jwt:Key` is missing from config, the fallback `"SuperSecretKeyForTicketSystem2024!@#$%"` is used — a static key that appears in source code. Any attacker can forge valid JWT tokens and impersonate any user, including admin.

**Remediation:**
1. Remove the hardcoded fallback keys — crash instead of silently using a weak key.
2. Use a 256-bit (32+ character) cryptographically random key generated at deploy time.
3. Store the signing key in an environment variable or Azure Key Vault. Never in `appsettings.json`.

---

#### Finding 3 — Missing Role-Based Authorization (Privilege Escalation)

| Field | Value |
|-------|-------|
| **Files** | All controllers in `Controllers/` |
| **CWE** | CWE-862 (Missing Authorization), CWE-269 (Privilege Escalation) |

Every controller uses only `[Authorize]` — never `[Authorize(Roles = "Admin")]`:

```csharp
[Authorize]                // <-- ANY authenticated user
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    => Ok(await _userService.CreateAsync(request));
```

**Exposed endpoints with no role check:**
| Endpoint | Action | Impact |
|----------|--------|--------|
| `POST /api/users` | Create user (any role) | SPOC can create admin accounts |
| `PUT /api/users/{id}` | Update any user | Modify role, email, password |
| `POST /api/users/{id}/reset-password` | Generate reset token for any user | Full account takeover |
| `DELETE /api/users/{id}` | Delete users | Data loss |
| `POST /api/applications` | Create/update apps | Modify routing, SPOC assignments |
| `POST /api/tickets/bulk-assign` | Bulk reassign tickets | Mass data manipulation |

**Remediation:**
1. Add `[Authorize(Roles = "Admin")]` to admin-level endpoints.
2. For user-facing endpoints (e.g., ticket CRUD), verify the requesting user owns or is assigned to the resource.
3. Implement a resource-based authorization handler.

---

### HIGH

#### Finding 4 — No Input Validation on Any DTO

| Field | Value |
|-------|-------|
| **Files** | All DTOs in `Models/DTOs/` |
| **CWE** | CWE-20 (Improper Input Validation), CWE-89 (SQL Injection) |

All request DTOs use plain auto-properties with NO data annotations:
```csharp
// Example: LoginRequest.cs
public string Email { get; set; } = string.Empty;    // No [Required], [EmailAddress], [StringLength]
public string Password { get; set; } = string.Empty;  // No [Required], [MinLength]
```

**Risk:** SQL injection (if interpolated into queries — though EF Core parameterization limits this), mass assignment, denial of service via massive payloads, business logic bypass.

**Remediation:** Add `[Required]`, `[EmailAddress]`, `[StringLength]`, `[Range]`, and `[RegularExpression]` attributes to all DTO properties. Use `[ApiController]` attribute (already present) which automatically returns 400 on validation failure — but only if validation attributes exist.

---

#### Finding 5 — JWT Tokens Stored in `localStorage`

| Field | Value |
|-------|-------|
| **File** | `frontend/src/contexts/AuthContext.jsx:27-29`, `frontend/src/services/apiClient.js:11` |
| **CWE** | CWE-312 (Cleartext Storage of Sensitive Information) |

```javascript
localStorage.setItem('accessToken', data.accessToken)
localStorage.setItem('refreshToken', data.refreshToken)
```

**Risk:** `localStorage` is accessible to any JavaScript executing in the same origin. A single stored-XSS vulnerability (even in a third-party widget) would exfiltrate both access and refresh tokens, granting persistent account takeover. Refresh tokens with 7-day expiry compound the risk.

**Remediation:**
1. Migrate to HttpOnly, Secure, SameSite=Strict cookies for token storage.
2. Set short access token expiry (done: 15 min) with refresh rotation.
3. Implement a backend endpoint that sets `Set-Cookie` headers server-side.

---

#### Finding 6 — No Rate Limiting on Login

| Field | Value |
|-------|-------|
| **File** | `AuthController.cs:16-28` |
| **CWE** | CWE-307 (Improper Restriction of Excessive Auth Attempts) |

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
    => Ok(await _authService.LoginAsync(request));
```

**Risk:** Unlimited brute-force password attempts against any account. No CAPTCHA, no IP-based throttling, no account lockout.

**Remediation:**
1. Add `[EnableRateLimiting]` using .NET 8 built-in rate limiting middleware.
2. Implement account lockout after 5 failed attempts (temporary, 15-minute cooldown).
3. Optional: add CAPTCHA (reCAPTCHA v3) after 3 failed attempts.

---

#### Finding 7 — User Enumeration Possible

| Field | Value |
|-------|-------|
| **File** | `AuthService.cs:24-31` |
| **CWE** | CWE-204 (Information Exposure Through Error Messages) |

```csharp
var user = await _userRepo.Query()
    .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive)
    ?? throw new UnauthorizedAccessException("Invalid email or password");

if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    throw new UnauthorizedAccessException("Invalid email or password");
```

**Risk:** While the error message is the same ("Invalid email or password") in both cases, timing differs — the DB query path vs BCrypt verify path have measurable time differences, enabling timing-based user enumeration. Additionally, the initial query reveals whether the email exists in the system.

**Remediation:**
1. Always perform both lookups (DB + verify) regardless — compute a dummy BCrypt hash when the user is not found.
2. Add a small random delay to make timing attacks infeasible.

---

#### Finding 8 — Weak / Predictable Default Passwords

| Field | Value |
|-------|-------|
| **Files** | `DbInitializer.cs:66`, `UserService.cs:49` |
| **CWE** | CWE-521 (Weak Password Requirements) |

```csharp
// DbInitializer.cs — admin seed
PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");

// UserService.cs — new user creation
PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
```

**Risk:** The default admin password (`Admin@123`) and new-user password (`Password123!`) are predictable and identical across all deployments. Anyone with source code access knows them. No "force password change on first login" mechanism exists.

**Remediation:**
1. Generate a cryptographically random password during first-time setup.
2. Require password change on first login (flag in `User` entity).
3. Enforce password complexity (min 12 chars, mixed case, digits, symbols).

---

#### Finding 9 — SSL/TLS Certificate Validation Disabled

| Field | Value |
|-------|-------|
| **File** | `EmailReceiverService.cs:70-75` |
| **CWE** | CWE-295 (Improper Certificate Validation) |

```csharp
_client.ServerCertificateValidationCallback = (sender, cert, chain, errors) => {
    if (sslPolicyErrors == SslPolicyErrors.None) return true;
    _logger.LogWarning("SSL validation failed: {Errors}. Accepting anyway.", errors);
    return true;   // <-- ALWAYS returns true, even on errors
};
```

**Risk:** Man-in-the-Middle (MITM) attacks against the IMAP connection to Gmail. An active network attacker can intercept all incoming support emails, including attachments and user-submitted sensitive data.

**Remediation:**
1. Remove the custom callback entirely — let MailKit's default SSL validation run.
2. If a custom callback is required for development, gate it behind `#if DEBUG`.

---

#### Finding 10 — Insecure Direct Object Reference (IDOR) on Tickets

| Field | Value |
|-------|-------|
| **File** | `TicketsController.cs:27-29` |
| **CWE** | CWE-639 (Insecure Direct Object Reference) |

```csharp
[HttpGet("{id:int}")]
public async Task<IActionResult> GetById(int id)
{
    var result = await _ticketService.GetByIdAsync(id);
    if (result == null) return NotFound();
    return Ok(result);
}
```

**Risk:** Any authenticated user can enumerate and view any ticket by iterating IDs. No check verifies the user is the assigned SPOC, the requester, or belongs to the relevant application's department.

**Remediation:**
1. For SPOC users: filter tickets to only those assigned to them.
2. For requester users: filter to tickets they created.
3. For admin users: allow full access (with proper role check).
4. Consider using GUID-based ticket identifiers instead of sequential integers.

---

### MEDIUM

#### Finding 11 — CORS Overly Permissive

**File:** `Program.cs:114-123` — `AllowAnyHeader()`, `AllowAnyMethod()` with `AllowCredentials()`. While specific origins are allowed, the header/method wildcards are unnecessary. Restrict to only needed headers and HTTP methods.

#### Finding 12 — HTTP (No TLS) in Configuration

**File:** `launchSettings.json:16` — Application URL is `http://localhost:5001`. All traffic is unencrypted. Add HTTPS binding and enforce redirect.

#### Finding 13 — Password Change Does Not Verify Current Password

**File:** `AuthController.cs:54-61` — The `ChangePassword` endpoint receives `CurrentPassword` and `NewPassword` but the service method `GeneratePasswordResetTokenAsync` ignores both and simply issues a reset token. Password change is functionally broken.

#### Finding 14 — Refresh Token Rotation Not Enforced

**File:** `AuthService.cs:60-93` — On refresh, a new token is issued but the old one is not invalidated until its 7-day expiry. Implement refresh token rotation (invalidate old, issue new) and track token family to detect reuse.

#### Finding 15 — No Account Lockout / Brute-Force Protection

**File:** `AuthService.cs:22-58` — No failed-attempt tracking. Add `FailedLoginAttempts` and `LockedUntilUtc` columns to the `User` entity.

#### Finding 16 — Missing HTTP Security Headers

**File:** `Program.cs` — No `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`, `Strict-Transport-Security`, or `Referrer-Policy` headers. Add via middleware or `web.config`.

#### Finding 17 — SignalR Hub — Missing `[Authorize]` + Group Enumeration

**File:** `TicketHub.cs` — No `[Authorize]` attribute on the hub class. The `JoinUserGroup(int userId)` method allows any client to join any user's notification group. Add `[Authorize]` and validate that the requesting user's ID matches the group ID.

#### Finding 18 — Audit Log IP Address Not Captured

**File:** `AuditService.cs` — All calls pass `null` for `ipAddress`. Capture `HttpContext.Connection.RemoteIpAddress` in controller base or middleware.

---

### LOW / INFO

#### Finding 19 — Large Default Page Size (10,000)

**File:** `QueryParams.cs:7` — `PageSize = 10000` allows single-request data exfiltration of the entire ticket table. Lower default to 20–50 and enforce a server-side maximum (e.g., 100).

#### Finding 20 — Known-Vulnerable Package Versions

- `Swashbuckle.AspNetCore 6.6.2` — has known CVEs. Update to latest.
- `BCrypt.Net-Next 4.2.0` — verify against latest advisory database.
- `@microsoft/signalr ^10.0.0` — confirm no known vulnerabilities.

#### Finding 21 — Hardcoded Admin Credentials in DbInitializer

`DbInitializer.cs:57-72` — Admin email (`admin@ticketingsystem.com`) and password (`Admin@123`) are in source. Seed from environment variables or a secure initial setup flow.

#### Finding 22 — No Email Verification for New Users

`UserService.cs:30-54` — Any email can be registered without verification. Add email confirmation flow before activation.

---

## Remediation Priority Matrix

| Priority | Findings | Effort | Impact |
|----------|----------|--------|--------|
| **P0 — Immediate** | Finding 1 (leaked secrets), Finding 2 (JWT key) | Minutes | Full system compromise |
| **P1 — Critical** | Finding 3 (role-based auth), Finding 10 (IDOR) | 2–3 days | Privilege escalation, data breach |
| **P2 — High** | Findings 4–9 | 3–5 days | Injection, token theft, MITM |
| **P3 — Medium** | Findings 11–18 | 3–5 days | Hardening, monitoring |
| **P4 — Low** | Findings 19–20 | 1 day | Performance, dependency hygiene |
| **P5 — Info** | Findings 21–22 | 1 day | Best practices |

---

## Methodology

- **Scope:** Static analysis of source code only (no live scanning, no penetration testing).
- **Standards:** OWASP Top 10 (2021), OWASP ASVS Level 1, CWE categorization.
- **Tools used:** Manual code review, pattern matching for hardcoded secrets, insecure crypto, missing auth.
- **Limitations:** No dynamic analysis, no dependency vulnerability scanner (e.g., no `dotnet list package --vulnerable` or `npm audit` output), no runtime testing.

---

*Report generated by static codebase analysis. All validations should be confirmed with dynamic testing before production deployment.*
