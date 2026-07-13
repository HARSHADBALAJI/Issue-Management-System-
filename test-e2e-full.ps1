param([int]$TicketId = 48)

$ApiBase = "http://localhost:5001/api"
$ErrorActionPreference = "Stop"
$passCount = 0; $failCount = 0

function Login {
  Write-Host "`n=== LOGIN ===" -ForegroundColor Cyan
  $r = Invoke-WebRequest -Uri "$ApiBase/auth/login" -Method POST -Headers @{"Content-Type"="application/json"} -Body '{"email":"admin@ticketingsystem.com","password":"Admin@123"}' -UseBasicParsing -TimeoutSec 15
  $script:token = ($r.Content | ConvertFrom-Json).accessToken
  $script:headers = @{Authorization="Bearer $script:token"}
  Write-Host "OK" -ForegroundColor Green
}

function GetTicket {
  param([int]$Id)
  $r = Invoke-WebRequest -Uri "$ApiBase/tickets/$Id" -Method GET -Headers $script:headers -UseBasicParsing -TimeoutSec 30
  return ($r.Content | ConvertFrom-Json)
}

function ApiPut {
  param([string]$Url, [string]$Body)
  try {
    $r = Invoke-WebRequest -Uri $Url -Method PUT -Headers $script:headers -Body $Body -ContentType "application/json" -UseBasicParsing -TimeoutSec 10
    return @{ok=$true; code=$r.StatusCode}
  } catch {
    $err = $_.Exception
    try { $reader = New-Object System.IO.StreamReader($err.Response.GetResponseStream()); $body = $reader.ReadToEnd() } catch { $body = "?" }
    return @{ok=$false; code=$err.Response.StatusCode; error=$body}
  }
}

function ApiPost {
  param([string]$Url, [string]$Body)
  try {
    $r = Invoke-WebRequest -Uri $Url -Method POST -Headers $script:headers -Body $Body -ContentType "application/json" -UseBasicParsing -TimeoutSec 10
    return @{ok=$true; code=$r.StatusCode}
  } catch {
    $err = $_.Exception
    try { $reader = New-Object System.IO.StreamReader($err.Response.GetResponseStream()); $body = $reader.ReadToEnd() } catch { $body = "?" }
    return @{ok=$false; code=$err.Response.StatusCode; error=$body}
  }
}

function DBQuery {
  param([string]$Q)
  try {
    Add-Type -AssemblyName System.Data -ErrorAction SilentlyContinue
    $conn = New-Object System.Data.SqlClient.SqlConnection("Server=localhost,1433;Database=ticketing_system;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True")
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $Q
    $reader = $cmd.ExecuteReader()
    $rows = @()
    while ($reader.Read()) {
      $row = @{}
      for ($i = 0; $i -lt $reader.FieldCount; $i++) {
        $name = $reader.GetName($i)
        $val = $reader.GetValue($i)
        if ($val -eq [System.DBNull]::Value) { $val = $null }
        $row[$name] = $val
      }
      $rows += $row
    }
    $reader.Close(); $conn.Close()
    return $rows
  } catch {
    Write-Host "  DB ERROR: $_" -ForegroundColor DarkYellow
    return $null
  }
}

function Check {
  param([string]$Label, [bool]$Cond)
  if ($Cond) { Write-Host "  PASS: $Label" -ForegroundColor Green; $script:passCount++ }
  else { Write-Host "  FAIL: $Label" -ForegroundColor Red; $script:failCount++ }
}

Login

# ===== GET BASELINE =====
Write-Host "`n=== BASELINE TICKET ===" -ForegroundColor Yellow
$t0 = GetTicket -Id $TicketId
Write-Host "Ticket: $($t0.ticketNumber) Status=$($t0.statusName) Assigned=$($t0.assignedToName)(ID=$($t0.assignedToId))"
Write-Host "Messages=$($t0.messages.Count) History=$($t0.statusHistory.Count) CA=$($t0.correctiveActions.Count) SLA=$($t0.slaDeadline)"

# ===== TEST 1: STATUS CHANGE OPEN -> IN_PROGRESS =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "TEST 1: STATUS CHANGE (open -> in_progress)" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$r = ApiPut -Url "$ApiBase/tickets/$TicketId/status" -Body '{"statusId":1,"remarks":"E2E test: status open->in_progress"}'
Check "API returns 204" ($r.ok -and $r.code -eq 204)

Start-Sleep 1
$t1 = GetTicket -Id $TicketId
Check "Ticket.StatusName=in_progress" ($t1.statusName -eq "in_progress")
$histMatch = $t1.statusHistory | Where-Object { $_.toStatus -eq "in_progress" -and $_.fromStatus -eq "open" }
Check "TicketStatusHistory entry created (open->in_progress)" ($histMatch.Count -ge 1)

# DB: Check Notifications & AuditLogs
$notifs = DBQuery "SELECT UserId, Type, Title FROM Notifications WHERE TicketId=$TicketId ORDER BY Id DESC"
if ($notifs) { Check "Notifications created for this status change" ($notifs.Count -ge 1) }
$audits = DBQuery "SELECT Action, OldValues, NewValues FROM AuditLogs WHERE EntityType='Ticket' AND EntityId=$TicketId ORDER BY Id DESC"
if ($audits) { Check "AuditLog entry created for status change" ($audits.Count -ge 1) }

# ===== TEST 2: STATUS CHANGE IN_PROGRESS -> RESOLVED =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "TEST 2: STATUS CHANGE (in_progress -> resolved)" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$r = ApiPut -Url "$ApiBase/tickets/$TicketId/status" -Body '{"statusId":3,"remarks":"E2E test: resolved"}'
Check "API returns 204" ($r.ok -and $r.code -eq 204)

Start-Sleep 1
$t2 = GetTicket -Id $TicketId
Check "Ticket.StatusName=resolved" ($t2.statusName -eq "resolved")
Check "ResolvedAt is set" ($t2.resolvedAt -ne $null)

$histMatch = $t2.statusHistory | Where-Object { $_.toStatus -eq "resolved" -and $_.fromStatus -eq "in_progress" }
Check "TicketStatusHistory entry (in_progress->resolved)" ($histMatch.Count -ge 1)

# ===== TEST 3: STATUS CHANGE RESOLVED -> CLOSED =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "TEST 3: STATUS CHANGE (resolved -> closed)" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$r = ApiPut -Url "$ApiBase/tickets/$TicketId/status" -Body '{"statusId":4,"remarks":"E2E test: closed"}'
Check "API returns 204" ($r.ok -and $r.code -eq 204)

Start-Sleep 1
$t3 = GetTicket -Id $TicketId
Check "Ticket.StatusName=closed" ($t3.statusName -eq "closed")
Check "ClosedAt is set" ($t3.closedAt -ne $null)

# ===== TEST 4: INVALID TRANSITION (closed -> open) =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "TEST 4: INVALID TRANSITION (closed -> open = 400)" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$r = ApiPut -Url "$ApiBase/tickets/$TicketId/status" -Body '{"statusId":5,"remarks":"should fail"}'
Check "API returns 400 for invalid transition" ($r.ok -eq $false -and $r.code -eq 400)
Check "Error message mentions status" ($r.error -match "status")

# ===== TEST 5: DUPLICATE STATUS =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "TEST 5: DUPLICATE STATUS (closed -> closed = 400)" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$r = ApiPut -Url "$ApiBase/tickets/$TicketId/status" -Body '{"statusId":4,"remarks":"duplicate"}'
Check "API returns 400 for duplicate status" ($r.ok -eq $false -and $r.code -eq 400)
Check "Error mentions already" ($r.error -match "already")

# ===== TEST 6: REOPEN =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "TEST 6: REOPEN TICKET" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$r = ApiPost -Url "$ApiBase/tickets/$TicketId/reopen" -Body '{}'
Check "API returns 200 for reopen" ($r.ok -and $r.code -eq 200)

Start-Sleep 1
$t4 = GetTicket -Id $TicketId
Check "Ticket.StatusName=in_progress after reopen" ($t4.statusName -eq "in_progress")
Check "ResolvedAt cleared after reopen" ($t4.resolvedAt -eq $null)
Check "ClosedAt cleared after reopen" ($t4.closedAt -eq $null)

$histMatch = $t4.statusHistory | Where-Object { $_.toStatus -eq "in_progress" -and $_.fromStatus -eq "closed" }
Check "TicketStatusHistory entry (closed->in_progress) for reopen" ($histMatch.Count -ge 1)

# ===== TEST 7: SPOC ASSIGN =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "TEST 7: SPOC ASSIGN (reassign to ID=4, Jayasmita)" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$r = ApiPut -Url "$ApiBase/tickets/$TicketId/assign" -Body '{"assignedToUserId":4}'
Check "API returns 204 for assign" ($r.ok -and $r.code -eq 204)

Start-Sleep 1
$t5 = GetTicket -Id $TicketId
Check "Ticket.AssignedToId=4 (Jayasmita)" ($t5.assignedToId -eq 4)

# ===== TEST 8: DUPLICATE ASSIGN =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "TEST 8: DUPLICATE ASSIGN = 400" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$r = ApiPut -Url "$ApiBase/tickets/$TicketId/assign" -Body '{"assignedToUserId":4}'
Check "API returns 400 for duplicate assign" ($r.ok -eq $false -and $r.code -eq 400)
Check "Error mentions already assigned" ($r.error -match "already assigned")

# ===== TEST 9: ADD MESSAGE =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "TEST 9: ADD MESSAGE (reply)" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$beforeMsgCount = $t5.messages.Count
$r = ApiPost -Url "$ApiBase/tickets/$TicketId/messages" -Body '{"content":"E2E test: reply message","isInternal":false}'
Check "API returns 200 for add message" ($r.ok -and $r.code -eq 200)

Start-Sleep 1
$t6 = GetTicket -Id $TicketId
Check "New message created" ($t6.messages.Count -eq $beforeMsgCount + 1)
$lastMsg = $t6.messages | Select-Object -Last 1
Check "Message content matches" ($lastMsg.content -eq "E2E test: reply message")
Check "Message has UserId (not RequesterId)" ($lastMsg.userId -ne $null)

# ===== TEST 10: ADD CORRECTIVE ACTION =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "TEST 10: ADD CORRECTIVE ACTION" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$beforeCACount = $t6.correctiveActions.Count
$r = ApiPost -Url "$ApiBase/tickets/$TicketId/corrective-actions" -Body '{"description":"E2E test: corrective action"}'
Check "API returns 200 for CA" ($r.ok -and $r.code -eq 200)

Start-Sleep 1
$t7 = GetTicket -Id $TicketId
Check "New CA created" ($t7.correctiveActions.Count -eq $beforeCACount + 1)
$lastCA = $t7.correctiveActions | Select-Object -Last 1
Check "CA description matches" ($lastCA.description -eq "E2E test: corrective action")
Check "CA has performedBy (userId linked)" ($lastCA.performedByUserId -gt 0)

# ===== TEST 11: SLA TRACKING =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "TEST 11: SLA TRACKING" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

try {
  $r = Invoke-WebRequest -Uri "$ApiBase/sla/tickets/$TicketId" -Method GET -Headers $script:headers -UseBasicParsing -TimeoutSec 10
  $sla = $r.Content | ConvertFrom-Json
  Check "SLA endpoint returns status" ($sla.status -ne $null)
  Check "SLA has deadlineAt" ($sla.deadlineAt -ne $null)
  Check "SLA has remainingTime" ($sla.remainingTime -ne $null)
} catch { Check "SLA endpoint accessible" $false }

# ===== FINAL DB CONSISTENCY CHECK =====
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host "FINAL DB CONSISTENCY CHECK" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan

$tickets = DBQuery "SELECT Id, TicketNumber, StatusId, AssignedToUserId, ResolvedAt, ClosedAt FROM Tickets WHERE Id=$TicketId"
if ($tickets) {
  $row = $tickets[0]
  Check "Ticket exists in DB" ($row.Id -eq $TicketId)
  Check "AssignedToUserId is valid FK (4)" ($row.AssignedToUserId -eq 4)
}

$hist = DBQuery "SELECT COUNT(*) as Cnt FROM TicketStatusHistory WHERE TicketId=$TicketId"
if ($hist) { Write-Host "  INFO: TicketStatusHistory count = $($hist[0].Cnt)" -ForegroundColor White }

$msgs = DBQuery "SELECT COUNT(*) as Cnt FROM TicketMessages WHERE TicketId=$TicketId"
if ($msgs) { Write-Host "  INFO: TicketMessages count = $($msgs[0].Cnt)" -ForegroundColor White }

$cas = DBQuery "SELECT COUNT(*) as Cnt FROM TicketCorrectiveActions WHERE TicketId=$TicketId"
if ($cas) { Write-Host "  INFO: TicketCorrectiveActions count = $($cas[0].Cnt)" -ForegroundColor White }

$notifs = DBQuery "SELECT COUNT(*) as Cnt FROM Notifications WHERE TicketId=$TicketId"
if ($notifs) { Write-Host "  INFO: Notifications count = $($notifs[0].Cnt)" -ForegroundColor White }

$audits = DBQuery "SELECT COUNT(*) as Cnt FROM AuditLogs WHERE EntityType='Ticket' AND EntityId=$TicketId"
if ($audits) { Write-Host "  INFO: AuditLogs count = $($audits[0].Cnt)" -ForegroundColor White }

$slas = DBQuery "SELECT COUNT(*) as Cnt FROM TicketSlas WHERE TicketId=$TicketId"
if ($slas) { Write-Host "  INFO: TicketSlas count = $($slas[0].Cnt)" -ForegroundColor White }

# Check FK integrity: every TicketMessage.UserId must exist in Users table
$orphanMsgs = DBQuery "SELECT COUNT(*) as Cnt FROM TicketMessages m LEFT JOIN Users u ON m.UserId=u.Id WHERE m.TicketId=$TicketId AND m.UserId IS NOT NULL AND u.Id IS NULL"
if ($orphanMsgs -and $orphanMsgs[0].Cnt -gt 0) { Check "No orphan TicketMessages (broken FK)" $false } else { Check "No orphan TicketMessages (broken FK)" $true }

$orphanCAs = DBQuery "SELECT COUNT(*) as Cnt FROM TicketCorrectiveActions ca LEFT JOIN Users u ON ca.PerformedByUserId=u.Id WHERE ca.TicketId=$TicketId AND u.Id IS NULL"
if ($orphanCAs -and $orphanCAs[0].Cnt -gt 0) { Check "No orphan CorrectiveActions (broken FK)" $false } else { Check "No orphan CorrectiveActions (broken FK)" $true }

# ===== SUMMARY =====
Write-Host "`n" -NoNewline
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "TEST SUMMARY" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Passed: $passCount" -ForegroundColor Green
Write-Host "Failed: $failCount" -ForegroundColor Red
Write-Host "Total:  $($passCount + $failCount)"
if ($failCount -eq 0) { Write-Host "ALL TESTS PASSED!" -ForegroundColor Green } else { Write-Host "SOME TESTS FAILED" -ForegroundColor Red }
