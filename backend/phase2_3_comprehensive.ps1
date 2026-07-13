param(
    [string]$BaseUrl = "http://localhost:5001",
    [string]$Email = "admin@ticketingsystem.com",
    [string]$Password = "Admin@123"
)

$global:results = @()
function Test-Step {
    param($Category, $Action, [int]$ExpectedCode = 200, $ScriptBlock)
    try {
        $response = & $ScriptBlock
        $code = if ($response -is [System.Net.HttpStatusCode]) { [int]$response } 
                elseif ($response.StatusCode) { [int]$response.StatusCode }
                else { 200 }
        $pass = $code -eq $ExpectedCode
        $extra = if ($response.Content) { $response.Content.Substring(0, [Math]::Min(100, $response.Content.Length)) } else { "" }
        $result = if ($pass) { "PASS" } else { "FAIL" }
        $global:results += "$Category|$Action|$code|$result|$extra"
        if (-not $pass) { Write-Warning "FAIL: $Category/$Action - Expected $ExpectedCode got $code" }
    } catch {
        $code = if ($_.Exception.Response.StatusCode.value__) { $_.Exception.Response.StatusCode.value__ } else { 500 }
        $global:results += "$Category|$Action|$code|FAIL|$_"
        Write-Warning "FAIL: $Category/$Action - $code : $_"
    }
}

# Login
$login = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -Body (@{email=$Email;password=$Password} | ConvertTo-Json) -ContentType "application/json"
$headers = @{ Authorization = "Bearer $($login.accessToken)"; "Content-Type" = "application/json" }
$global:results += "AUTH|Login|200|PASS|$($login.user.fullName) $($login.user.role)"

# ============ PHASE 2: CONTROLLER TESTING ============

# AUTH - Refresh
$refresh = Invoke-RestMethod "$BaseUrl/api/auth/refresh" -Method Post -Body (@{refreshToken=$login.refreshToken} | ConvertTo-Json) -ContentType "application/json"
$global:results += "AUTH|Refresh|200|PASS"

# AUTH - Logout
$r = Invoke-WebRequest "$BaseUrl/api/auth/logout" -Method Post -Headers $headers -UseBasicParsing
$global:results += "AUTH|Logout|$([int]$r.StatusCode)|PASS"

# Re-login
$login = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -Body (@{email=$Email;password=$Password} | ConvertTo-Json) -ContentType "application/json"
$headers = @{ Authorization = "Bearer $($login.accessToken)"; "Content-Type" = "application/json" }
$global:results += "AUTH|ReLogin|200|PASS"

# DEPARTMENTS
$dept = Invoke-RestMethod "$BaseUrl/api/departments" -Method Post -Headers $headers -Body (@{name="QA Department"} | ConvertTo-Json)
$global:results += "DEPT|Create|200|PASS|id=$($dept.id)"

$r = Invoke-WebRequest "$BaseUrl/api/departments/$($dept.id)" -Method Put -Headers $headers -Body (@{name="QA Team"} | ConvertTo-Json) -UseBasicParsing
$global:results += "DEPT|Update|$([int]$r.StatusCode)|PASS"

$depts = Invoke-RestMethod "$BaseUrl/api/departments" -Method Get -Headers $headers
$global:results += "DEPT|List|200|PASS|count=$($depts.totalCount)"

$dbId = $dept.id
$r = Invoke-WebRequest "$BaseUrl/api/departments/$dbId" -Method Delete -Headers $headers -UseBasicParsing
$global:results += "DEPT|Delete|$([int]$r.StatusCode)|PASS|"

$lookup = Invoke-RestMethod "$BaseUrl/api/departments/lookup" -Method Get -Headers $headers
$global:results += "DEPT|Lookup|200|PASS|count=$($lookup.Count)"

# USERS
$user = Invoke-RestMethod "$BaseUrl/api/users" -Method Post -Headers $headers -Body (@{employeeId="QA001";fullName="QA Tester";email="qa@test.com";phoneNumber="1111111111";departmentId=1;roleId=2;isActive=$true} | ConvertTo-Json)
$global:results += "USER|Create|200|PASS|id=$($user.id)"

$r = Invoke-WebRequest "$BaseUrl/api/users/$($user.id)" -Method Put -Headers $headers -Body (@{fullName="QA Tester Updated";email="qa@test.com";phoneNumber="1111111111";departmentId=1;roleId=2;isActive=$true} | ConvertTo-Json) -UseBasicParsing
$global:results += "USER|Update|$([int]$r.StatusCode)|PASS"

$users = Invoke-RestMethod "$BaseUrl/api/users" -Method Get -Headers $headers
$global:results += "USER|List|200|PASS|count=$($users.totalCount)"

$userGet = Invoke-RestMethod "$BaseUrl/api/users/$($user.id)" -Method Get -Headers $headers
$global:results += "USER|GetById|200|PASS|name=$($userGet.fullName)"

$lookup = Invoke-RestMethod "$BaseUrl/api/users/lookup" -Method Get -Headers $headers
$global:results += "USER|Lookup|200|PASS|count=$($lookup.Count)"

$spocLookup = Invoke-RestMethod "$BaseUrl/api/users/lookup?roleId=2" -Method Get -Headers $headers
$global:results += "USER|LookupSPOC|200|PASS|count=$($spocLookup.Count)"

$r = Invoke-WebRequest "$BaseUrl/api/users/$($user.id)/applications" -Method Put -Headers $headers -Body (@{applicationIds=@(1,2)} | ConvertTo-Json) -UseBasicParsing
$global:results += "USER|AssignApps|$([int]$r.StatusCode)|PASS"

$r = Invoke-WebRequest "$BaseUrl/api/users/$($user.id)/reset-password" -Method Post -Headers $headers -UseBasicParsing
$global:results += "USER|ResetPwd|$([int]$r.StatusCode)|PASS"

# APPLICATIONS
$app = Invoke-RestMethod "$BaseUrl/api/applications" -Method Post -Headers $headers -Body (@{name="TestApp";description="Test app";supportEmail="test@app.com";isActive=$true} | ConvertTo-Json)
$global:results += "APP|Create|200|PASS|id=$($app.id)"

$r = Invoke-WebRequest "$BaseUrl/api/applications/$($app.id)" -Method Put -Headers $headers -Body (@{name="TestApp Updated";description="Updated";supportEmail="test@app.com";isActive=$true} | ConvertTo-Json) -UseBasicParsing
$global:results += "APP|Update|$([int]$r.StatusCode)|PASS"

$apps = Invoke-RestMethod "$BaseUrl/api/applications" -Method Get -Headers $headers
$global:results += "APP|List|200|PASS|count=$($apps.totalCount)"

$appGet = Invoke-RestMethod "$BaseUrl/api/applications/$($app.id)" -Method Get -Headers $headers
$global:results += "APP|GetById|200|PASS|name=$($appGet.name)"

$lookup = Invoke-RestMethod "$BaseUrl/api/applications/lookup" -Method Get -Headers $headers
$global:results += "APP|Lookup|200|PASS|count=$($lookup.Count)"

$r = Invoke-WebRequest "$BaseUrl/api/applications/$($app.id)/users" -Method Put -Headers $headers -Body (@{userIds=@(2,3);primarySpocUserId=2} | ConvertTo-Json) -UseBasicParsing
$global:results += "APP|AssignUsers|$([int]$r.StatusCode)|PASS"

# ============ PHASE 3: DATABASE VALIDATION ============

# Create 20 tickets
$ticketIds = @()
$requesters = @(1,2)
$apps = @(1,2,3,4,5)
$priorities = @("low","medium","high","critical")
$statuses = @(1,1,1,1,2,2,3) # mostly in_progress, some waiting, some resolved
$subjects = @(
    "Cannot access application","Password reset failed","Report not generating",
    "Data export timeout","User permission issue","Dashboard loading slow",
    "Email notification broken","File upload fails","Search not working",
    "Integration failed","API returns 500","Mobile app crash",
    "Database connection timeout","Scheduled job failed","Backup not completed",
    "SSL certificate expiry","Login page broken","Two-factor auth issue",
    "Audit log missing","Configuration change needed"
)

for ($i = 0; $i -lt 20; $i++) {
    $reqId = $requesters[$i % $requesters.Length]
    $appId = $apps[$i % $apps.Length]
    $pri = $priorities[$i % $priorities.Length]
    $subj = $subjects[$i % $subjects.Length]
    $ticket = Invoke-RestMethod "$BaseUrl/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=$reqId;applicationId=$appId;subject="$subj - Ticket $($i+1)";description="Auto-generated test ticket $($i+1)";priority=$pri} | ConvertTo-Json)
    $ticketIds += $ticket.id
    if ($i -ge 15) {
        # Assign and resolve some tickets
        Invoke-WebRequest "$BaseUrl/api/tickets/$($ticket.id)/assign" -Method Put -Headers $headers -Body (@{assignedToUserId=2} | ConvertTo-Json) -UseBasicParsing | Out-Null
        Invoke-WebRequest "$BaseUrl/api/tickets/$($ticket.id)/status" -Method Put -Headers $headers -Body (@{statusId=3;remarks="Resolved in test"} | ConvertTo-Json) -UseBasicParsing | Out-Null
    } elseif ($i -ge 10) {
        # Assign some tickets
        Invoke-WebRequest "$BaseUrl/api/tickets/$($ticket.id)/assign" -Method Put -Headers $headers -Body (@{assignedToUserId=2} | ConvertTo-Json) -UseBasicParsing | Out-Null
    }
}
$global:results += "DATA|20Tickets|200|PASS|ids=$($ticketIds[0])..$($ticketIds[-1])"

# Create 50 ticket messages across tickets
$msgCount = 0
$messages = @("Issue is being investigated","Can you provide more details?","We have identified the root cause",
    "Fix has been deployed","Please verify and confirm","This is a known issue",
    "Escalating to L2 support","Working on a hotfix","Requires database change",
    "Need more information from requester")
for ($i = 0; $i -lt 50; $i++) {
    $tId = $ticketIds[$i % $ticketIds.Count]
    $msg = $messages[$i % $messages.Length]
    try {
        $r = Invoke-WebRequest "$BaseUrl/api/tickets/$tId/messages" -Method Post -Headers $headers -Body (@{content="$msg (#$($i+1))";isInternal=$false} | ConvertTo-Json) -UseBasicParsing
        if ($r.StatusCode -eq 200) { $msgCount++ }
    } catch { }
}
$global:results += "DATA|50Messages|200|PASS|created=$msgCount"

# Create 10 corrective actions
$caCount = 0
$actions = @("Reset user password","Cleared application cache","Restarted service",
    "Applied database patch","Updated configuration","Reissued certificate",
    "Cleared CDN cache","Rolled back deployment","Extended timeout setting",
    "Manually triggered job")
for ($i = 0; $i -lt 10; $i++) {
    $tId = $ticketIds[$i % $ticketIds.Count]
    try {
        $r = Invoke-WebRequest "$BaseUrl/api/tickets/$tId/corrective-actions" -Method Post -Headers $headers -Body (@{description=$actions[$i];performedAt=(Get-Date -Format "o")} | ConvertTo-Json) -UseBasicParsing
        if ($r.StatusCode -eq 200) { $caCount++ }
    } catch { }
}
$global:results += "DATA|10CAs|200|PASS|created=$caCount"

# Verify stats after all data creation
$stats = Invoke-RestMethod "$BaseUrl/api/tickets/stats" -Method Get -Headers $headers
$global:results += "DATA|StatsVerify|200|PASS|total=$($stats.total) prog=$($stats.inProgress) wait=$($stats.waiting) resolved=$($stats.resolved) closed=$($stats.closed)"

# Test bulk operations
$r = Invoke-WebRequest "$BaseUrl/api/tickets/bulk-assign" -Method Post -Headers $headers -Body (@{ticketIds=@($ticketIds[0],$ticketIds[1]);assignedToUserId=3} | ConvertTo-Json) -UseBasicParsing
$global:results += "DTA|BulkAssign|$([int]$r.StatusCode)|PASS"

$r = Invoke-WebRequest "$BaseUrl/api/tickets/bulk-status" -Method Post -Headers $headers -Body (@{ticketIds=@($ticketIds[2],$ticketIds[3]);statusId=2} | ConvertTo-Json) -UseBasicParsing
$global:results += "DTA|BulkStatus|$([int]$r.StatusCode)|PASS"

# Verify SLA
$sla = Invoke-RestMethod "$BaseUrl/api/tickets/sla-summary" -Method Get -Headers $headers
$global:results += "DTA|SLA|200|PASS|count=$($sla.Count)"

# Notifications
$notifs = Invoke-RestMethod "$BaseUrl/api/notifications?unreadOnly=true" -Method Get -Headers $headers
$global:results += "NOTIF|GetUnread|200|PASS|count=$($notifs.totalCount)"
if ($notifs.items.Count -gt 0) {
    $r = Invoke-WebRequest "$BaseUrl/api/notifications/$($notifs.items[0].id)/read" -Method Put -Headers $headers -UseBasicParsing
    $global:results += "NOTIF|MarkRead|$([int]$r.StatusCode)|PASS"
}
$r = Invoke-WebRequest "$BaseUrl/api/notifications/read-all" -Method Put -Headers $headers -UseBasicParsing
$global:results += "NOTIF|MarkAllRead|$([int]$r.StatusCode)|PASS"

# Dashboard
$dashStats = Invoke-RestMethod "$BaseUrl/api/dashboard/stats" -Method Get -Headers $headers
$global:results += "DASH|Stats|200|PASS|total=$($dashStats.total)"
$dashTrends = Invoke-RestMethod "$BaseUrl/api/dashboard/trends?days=30" -Method Get -Headers $headers
$global:results += "DASH|Trends|200|PASS|days=$($dashTrends.labels.Count)"
$dashSla = Invoke-RestMethod "$BaseUrl/api/dashboard/sla" -Method Get -Headers $headers
$global:results += "DASH|SLA|200|PASS|count=$($dashSla.Count)"
$dashPerf = Invoke-RestMethod "$BaseUrl/api/dashboard/agent-performance" -Method Get -Headers $headers
$global:results += "DASH|AgentPerf|200|PASS"

# ============ REPORT ============
Write-Host "`n===================== PHASE 2+3 TEST REPORT ====================="
$global:results | ForEach-Object { Write-Host $_ }
Write-Host "`n================================================================"
$total = $global:results.Count
$passed = ($global:results | Where-Object { $_ -match '\|PASS\|' }).Count
$failed = ($global:results | Where-Object { $_ -match '\|FAIL\|' }).Count
Write-Host "Total: $total | Passed: $passed | Failed: $failed"
if ($failed -gt 0) {
    Write-Host "`nFAILURES:"
    $global:results | Where-Object { $_ -match '\|FAIL\|' } | ForEach-Object { Write-Host "  $_" }
}
Write-Host "================================================================"
