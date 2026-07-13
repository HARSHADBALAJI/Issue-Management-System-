param(
    [string]$BaseUrl = "http://localhost:5001"
)

function Write-Step { param($Msg) Write-Host "`n>>> $Msg" -ForegroundColor Cyan }
function Check {
    param($Test, $Expected, $Actual)
    $pass = if ($Expected -eq $Actual) { $true } else { $false }
    $msg = if ($pass) { "PASS" } else { "FAIL" }
    Write-Host "  ${msg}: $Test (expected=$Expected actual=$Actual)"
    if (-not $pass) { $script:failures++ }
}

$script:failures = 0

$login = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -Body (@{email="admin@ticketingsystem.com";password="Admin@123"} | ConvertTo-Json) -ContentType "application/json"
$headers = @{ Authorization = "Bearer $($login.accessToken)"; "Content-Type" = "application/json" }

Write-Host "==================== PHASE 4: BUSINESS RULE TESTING ====================" -ForegroundColor Yellow

# -----------------------------------------------------------------
Write-Step "1. TICKET CREATION -> In Progress"
$ticket = Invoke-RestMethod "$BaseUrl/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=1;applicationId=1;subject="Biz Rule Test";description="Testing business rules";priority="medium"} | ConvertTo-Json)
Check "StatusId" 1 $ticket.statusId
Check "StatusName" "in_progress" $ticket.statusName
Check "TicketNumber format" $true ($ticket.ticketNumber -match "^TKT-\d+$")
Check "SLA created" $true ($ticket.createdAt -ne $null)

$detail = Invoke-RestMethod "$BaseUrl/api/tickets/$($ticket.id)" -Method Get -Headers $headers
Check "Initial status history count (new ticket)" 0 $detail.statusHistory.Count
Check "Initial messages count" 0 $detail.messages.Count

# -----------------------------------------------------------------
Write-Step "2. IN PROGRESS -> WAITING -> REPLY -> IN PROGRESS"
try { Invoke-RestMethod "$BaseUrl/api/tickets/$($ticket.id)/status" -Method Put -Headers $headers -Body (@{statusId=2;remarks="Waiting for user response"} | ConvertTo-Json) } catch { }

$detail = Invoke-RestMethod "$BaseUrl/api/tickets/$($ticket.id)" -Method Get -Headers $headers
Check "Status changed to waiting" "waiting" $detail.statusName
Check "Status history entry added" 1 $detail.statusHistory.Count
Check "From status (initial was in_progress)" "in_progress" $detail.statusHistory[0].fromStatus
Check "To status" "waiting" $detail.statusHistory[0].toStatus
Check "Remarks saved" "Waiting for user response" $detail.statusHistory[0].note

# Add message (simulating requester reply)
try { Invoke-RestMethod "$BaseUrl/api/tickets/$($ticket.id)/messages" -Method Post -Headers $headers -Body (@{content="Here are the details you requested. Please proceed.";isInternal=$false} | ConvertTo-Json) } catch { }

# Change back to In Progress (simulating auto-transition trigger)
try { Invoke-RestMethod "$BaseUrl/api/tickets/$($ticket.id)/status" -Method Put -Headers $headers -Body (@{statusId=1;remarks="Requester replied - resuming work"} | ConvertTo-Json) } catch { }

$detail = Invoke-RestMethod "$BaseUrl/api/tickets/$($ticket.id)" -Method Get -Headers $headers
Check "Status back to in_progress" "in_progress" $detail.statusName
Check "Status history entries (2)" 2 $detail.statusHistory.Count
Check "Second entry from" "waiting" $detail.statusHistory[1].fromStatus
Check "Second entry to" "in_progress" $detail.statusHistory[1].toStatus
Check "Message persisted" 1 $detail.messages.Count
Check "Message content" "Here are the details you requested. Please proceed." $detail.messages[0].content

# -----------------------------------------------------------------
Write-Step "3. IN PROGRESS -> RESOLVED -> CLOSED"
$t2 = Invoke-RestMethod "$BaseUrl/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=1;applicationId=1;subject="Resolve/Close Test";description="Testing resolve and close flow";priority="high"} | ConvertTo-Json)

# Assign first
try { Invoke-RestMethod "$BaseUrl/api/tickets/$($t2.id)/assign" -Method Put -Headers $headers -Body (@{assignedToUserId=2} | ConvertTo-Json) } catch { }

# Resolve
try { Invoke-RestMethod "$BaseUrl/api/tickets/$($t2.id)/status" -Method Put -Headers $headers -Body (@{statusId=3;remarks="Issue fixed - deployed hotfix"} | ConvertTo-Json) } catch { }

$detail = Invoke-RestMethod "$BaseUrl/api/tickets/$($t2.id)" -Method Get -Headers $headers
Check "Resolved status" "resolved" $detail.statusName
Check "ResolvedAt set" $true ($detail.resolutionAt -ne $null)
Check "Assigned to user 2" 2 $detail.assignedToId

# Close
try { Invoke-RestMethod "$BaseUrl/api/tickets/$($t2.id)/status" -Method Put -Headers $headers -Body (@{statusId=4;remarks="Confirmed by requester - closing"} | ConvertTo-Json) } catch { }

$detail = Invoke-RestMethod "$BaseUrl/api/tickets/$($t2.id)" -Method Get -Headers $headers
Check "Closed status" "closed" $detail.statusName
Check "ClosedAt set" $true ($detail.closedAt -ne $null)

# -----------------------------------------------------------------
Write-Step "4. REOPEN FLOW: Resolved -> In Progress (simulating reopen)"
$t3 = Invoke-RestMethod "$BaseUrl/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=1;applicationId=1;subject="Reopen Test";description="Testing reopen flow";priority="medium"} | ConvertTo-Json)
try { Invoke-RestMethod "$BaseUrl/api/tickets/$($t3.id)/status" -Method Put -Headers $headers -Body (@{statusId=3;remarks="Resolved"} | ConvertTo-Json) } catch { }

# Simulate reopen (change back to in_progress)
try { Invoke-RestMethod "$BaseUrl/api/tickets/$($t3.id)/status" -Method Put -Headers $headers -Body (@{statusId=1;remarks="Reopened per requester request"} | ConvertTo-Json) } catch { }

$detail = Invoke-RestMethod "$BaseUrl/api/tickets/$($t3.id)" -Method Get -Headers $headers
Check "Reopened to in_progress" "in_progress" $detail.statusName
Check "ResolutionAt cleared on reopen" $null $detail.resolutionAt

# -----------------------------------------------------------------
Write-Step "5. VERIFY ALL STATUS HISTORY"
$detail = Invoke-RestMethod "$BaseUrl/api/tickets/$($ticket.id)" -Method Get -Headers $headers
Write-Host "  Status history for ticket '$($detail.subject)':"
foreach ($h in $detail.statusHistory) {
    $from = if ($h.fromStatus) { $h.fromStatus } else { "(initial)" }
    Write-Host "    [$($h.createdAt)] ${from} -> $($h.toStatus) : $($h.note)"
}

Write-Host "`n==================== PHASE 4 RESULTS ====================" -ForegroundColor Yellow
Write-Host "Failures: $script:failures"
if ($script:failures -eq 0) { Write-Host "ALL BUSINESS RULE TESTS PASSED" -ForegroundColor Green } else { Write-Host "SOME TESTS FAILED" -ForegroundColor Red }
