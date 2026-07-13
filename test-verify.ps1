param([int]$TicketId = 94)

$ApiBase = "http://localhost:5001/api"
$ErrorActionPreference = "Stop"

function Login {
  Write-Host "LOGIN..." -ForegroundColor Cyan
  $r = Invoke-WebRequest -Uri "$ApiBase/auth/login" -Method POST -Headers @{"Content-Type"="application/json"} -Body '{"email":"admin@ticketingsystem.com","password":"Admin@123"}' -UseBasicParsing -TimeoutSec 15
  $script:token = ($r.Content | ConvertFrom-Json).accessToken
  $script:headers = @{Authorization="Bearer $script:token"}
  Write-Host "Token OK" -ForegroundColor Green
}

function Get-Ticket {
  param([int]$Id)
  $r = Invoke-WebRequest -Uri "$ApiBase/tickets/$Id" -Method GET -Headers $script:headers -UseBasicParsing -TimeoutSec 30
  return ($r.Content | ConvertFrom-Json)
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
      for ($i = 0; $i -lt $reader.FieldCount; $i++) { $row[$reader.GetName($i)] = $reader.GetValue($i) }
      $rows += $row
    }
    $reader.Close(); $conn.Close()
    return $rows
  } catch { return $null }
}

Login

# ===== GET BASELINE =====
Write-Host "`n=== BASELINE ===" -ForegroundColor Yellow
$t = Get-Ticket -Id $TicketId
Write-Host "Ticket: $($t.ticketNumber) | Status: $($t.statusName) | Assigned: $($t.assignedToName) (ID=$($t.assignedToUserId))"
Write-Host "Messages: $($t.messages.Count) | History: $($t.statusHistory.Count) | CA: $($t.correctiveActions.Count)"

# ===== TEST 1: STATUS CHANGE =====
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "TEST 1: STATUS CHANGE (open -> in_progress)" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan

$beforeStatusCount = $t.statusHistory.Count

try {
  $r = Invoke-WebRequest -Uri "$ApiBase/tickets/$TicketId/status" -Method PUT -Headers $script:headers -Body '{"statusId":1,"remarks":"E2E test: status change"}' -ContentType "application/json" -UseBasicParsing -TimeoutSec 10
  Write-Host "API: $($r.StatusCode) $($r.StatusDescription)" -ForegroundColor Green
} catch {
  Write-Host "FAILED: $_" -ForegroundColor Red
  try { $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream()); Write-Host "BODY: $($reader.ReadToEnd())" } catch {}
}

Start-Sleep 1
$t = Get-Ticket -Id $TicketId

# Verify via API
$pass = $true
if ($t.statusName -eq "in_progress") { Write-Host "PASS: Tickets.StatusId = in_progress" -ForegroundColor Green } else { Write-Host "FAIL: Tickets.StatusId = $($t.statusName)" -ForegroundColor Red; $pass = $false }

$newHist = $t.statusHistory | Where-Object { $_.toStatus -eq "in_progress" -and $_.fromStatus -eq "open" }
if ($newHist.Count -ge 1) { Write-Host "PASS: TicketStatusHistory entry created (open->in_progress)" -ForegroundColor Green } else { Write-Host "FAIL: No TicketStatusHistory entry" -ForegroundColor Red; $pass = $false }

# Verify via DB
Write-Host "`n-- DB Verification --" -ForegroundColor White
$rows = DBQuery "SELECT Id, StatusId, AssignedToUserId, ResolvedAt, ClosedAt, UpdatedAt FROM Tickets WHERE Id=$TicketId"
if ($rows) { $row = $rows[0]; Write-Host "Tickets: StatusId=$($row.StatusId) ResolvedAt=$($row.ResolvedAt) ClosedAt=$($row.ClosedAt)" }

$rows = DBQuery "SELECT TOP 3 Id, TicketId, FromStatusId, ToStatusId, ChangedByUserId, CreatedAt FROM TicketStatusHistory WHERE TicketId=$TicketId ORDER BY Id DESC"
if ($rows) { foreach ($r in $rows) { Write-Host "Hist: FromStatus=$($r.FromStatusId) ToStatus=$($r.ToStatusId) ChangedBy=$($r.ChangedByUserId)" } }

$rows = DBQuery "SELECT TOP 3 Id, UserId, TicketId, Type, Title, CreatedAt FROM Notifications WHERE TicketId=$TicketId ORDER BY Id DESC"
if ($rows) { foreach ($r in $rows) { Write-Host "Notif: User=$($r.UserId) Type=$($r.Type) Title=$($r.Title)" } } else { Write-Host "Notifications: (no access)" }

$rows = DBQuery "SELECT TOP 3 Id, UserId, Action, EntityType, EntityId, OldValues, NewValues, CreatedAt FROM AuditLogs WHERE EntityType='Ticket' AND EntityId=$TicketId ORDER BY Id DESC"
if ($rows) { foreach ($r in $rows) { Write-Host "Audit: User=$($r.UserId) Action=$($r.Action) Old=$($r.OldValues) New=$($r.NewValues)" } } else { Write-Host "AuditLogs: (no access)" }

if ($pass) { Write-Host "RESULT: STATUS CHANGE = PASS" -ForegroundColor Green } else { Write-Host "RESULT: STATUS CHANGE = FAIL" -ForegroundColor Red }
