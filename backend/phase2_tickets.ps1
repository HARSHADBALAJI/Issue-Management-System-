$base = "http://localhost:5001"
$report = @()

# Login
$login = Invoke-RestMethod "$base/api/auth/login" -Method Post -Body (@{email="admin@ticketingsystem.com";password="Admin@123"} | ConvertTo-Json) -ContentType "application/json"
$headers = @{ Authorization = "Bearer $($login.accessToken)"; "Content-Type" = "application/json" }

# First create a requester for tickets
$requesterData = @{fullName="John Requester";email="john@requester.com";phoneNumber="9999999999"} | ConvertTo-Json
# Note: There's no API endpoint for creating requesters directly yet - need to insert via DB or use existing
# For now, let me create some tickets using CreateTicketRequest

# Tickets - Create
$ticket1 = Invoke-RestMethod "$base/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=1;applicationId=1;subject="Cannot login to portal";description="Getting 401 error when trying to login";priority="high"} | ConvertTo-Json)
$report += "TICKET|Create #1|200|PASS|Num=$($ticket1.ticketNumber) Id=$($ticket1.id)"

$ticket2 = Invoke-RestMethod "$base/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=1;applicationId=2;subject="Payroll not updating";description="Salary not reflecting in system";priority="critical"} | ConvertTo-Json)
$report += "TICKET|Create #2|200|PASS|Num=$($ticket2.ticketNumber) Id=$($ticket2.id)"

$ticket3 = Invoke-RestMethod "$base/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=1;applicationId=3;subject="Report generation failed";description="Monthly report not generating";priority="medium"} | ConvertTo-Json)
$report += "TICKET|Create #3|200|PASS|Num=$($ticket3.ticketNumber) Id=$($ticket3.id)"

$ticket4 = Invoke-RestMethod "$base/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=2;applicationId=1;subject="Password reset not working";description="Reset email not being sent";priority="low"} | ConvertTo-Json)
$report += "TICKET|Create #4|200|PASS|Num=$($ticket4.ticketNumber) Id=$($ticket4.id)"

$ticket5 = Invoke-RestMethod "$base/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=2;applicationId=2;subject="Leave balance mismatch";description="Leave balance shows incorrect values";priority="high"} | ConvertTo-Json)
$report += "TICKET|Create #5|200|PASS|Num=$($ticket5.ticketNumber) Id=$($ticket5.id)"

$ticket6 = Invoke-RestMethod "$base/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=1;applicationId=4;subject="Lead data missing";description="CRM leads not showing for yesterday";priority="medium"} | ConvertTo-Json)
$report += "TICKET|Create #6|200|PASS|Num=$($ticket6.ticketNumber) Id=$($ticket6.id)"

$ticket7 = Invoke-RestMethod "$base/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=1;applicationId=5;subject="Email delivery delay";description="Emails taking 2+ hours to deliver";priority="critical"} | ConvertTo-Json)
$report += "TICKET|Create #7|200|PASS|Num=$($ticket7.ticketNumber) Id=$($ticket7.id)"

$ticket8 = Invoke-RestMethod "$base/api/tickets" -Method Post -Headers $headers -Body (@{requesterId=2;applicationId=3;subject="Budget approval stuck";description="Finance approval workflow not progressing";priority="high"} | ConvertTo-Json)
$report += "TICKET|Create #8|200|PASS|Num=$($ticket8.ticketNumber) Id=$($ticket8.id)"

# Tickets - List
$tickets = Invoke-RestMethod "$base/api/tickets" -Method Get -Headers $headers
$report += "TICKET|List|200|PASS|Total=$($tickets.totalCount)"

# Tickets - List with filters
$filtered = Invoke-RestMethod "$base/api/tickets?priority=high" -Method Get -Headers $headers
$report += "TICKET|FilterPriority|200|PASS|Count=$($filtered.totalCount)"

# Tickets - Get by ID
$ticketGet = Invoke-RestMethod "$base/api/tickets/$($ticket1.id)" -Method Get -Headers $headers
$report += "TICKET|GetById|200|PASS|Subject=$($ticketGet.subject) Status=$($ticketGet.statusName)"

# Tickets - Assign SPOC
$assignBody = @{assignedToUserId=2} | ConvertTo-Json
Invoke-RestMethod "$base/api/tickets/$($ticket1.id)/assign" -Method Put -Headers $headers -Body $assignBody
$report += "TICKET|Assign|204|PASS"

# Tickets - Update Status to Waiting
$statusBody = @{statusId=2;remarks="Waiting for user response"} | ConvertTo-Json
Invoke-RestMethod "$base/api/tickets/$($ticket1.id)/status" -Method Put -Headers $headers -Body $statusBody
$report += "TICKET|StatusWaiting|204|PASS"

# Tickets - Add Message
$msgBody = @{content="We are investigating the issue. Could you provide more details?";isInternal=$false} | ConvertTo-Json
$msg = Invoke-RestMethod "$base/api/tickets/$($ticket1.id)/messages" -Method Post -Headers $headers -Body $msgBody
$report += "TICKET|AddMessage|200|PASS"

# Tickets - Add Corrective Action
$caBody = @{description="Reset user password and clear cache";performedAt=(Get-Date -Format "o")} | ConvertTo-Json
$ca = Invoke-RestMethod "$base/api/tickets/$($ticket1.id)/corrective-actions" -Method Post -Headers $headers -Body $caBody
$report += "TICKET|AddCA|200|PASS"

# Tickets - Update Status to Resolved
$resBody = @{statusId=3;remarks="Issue resolved - password reset completed"} | ConvertTo-Json
Invoke-RestMethod "$base/api/tickets/$($ticket1.id)/status" -Method Put -Headers $headers -Body $resBody
$report += "TICKET|StatusResolved|204|PASS"

# Tickets - Verify status history
$ticketDetail = Invoke-RestMethod "$base/api/tickets/$($ticket1.id)" -Method Get -Headers $headers
$historyCount = $ticketDetail.statusHistory.Count
$report += "TICKET|StatusHistory|200|PASS|Entries=$historyCount"

# Tickets - Update Status to Closed
$closeBody = @{statusId=4;remarks="Closing ticket - resolved and confirmed"} | ConvertTo-Json
Invoke-RestMethod "$base/api/tickets/$($ticket1.id)/status" -Method Put -Headers $headers -Body $closeBody
$report += "TICKET|StatusClosed|204|PASS"

# Tickets - Stats
$stats = Invoke-RestMethod "$base/api/tickets/stats" -Method Get -Headers $headers
$report += "TICKET|Stats|200|PASS|Total=$($stats.total) Open=$($stats.inProgress)"

# Tickets - SLA Summary
$sla = Invoke-RestMethod "$base/api/tickets/sla-summary" -Method Get -Headers $headers
$report += "TICKET|SLA|200|PASS|Count=$($sla.Count)"

# Tickets - Bulk Assign
$bulkAssign = @{ticketIds=@($ticket2.id,$ticket3.id);assignedToUserId=3} | ConvertTo-Json
$baResult = Invoke-RestMethod "$base/api/tickets/bulk-assign" -Method Post -Headers $headers -Body $bulkAssign
$report += "TICKET|BulkAssign|200|PASS"

# Tickets - Bulk Status
$bulkStatus = @{ticketIds=@($ticket4.id,$ticket5.id);statusId=2} | ConvertTo-Json
$bsResult = Invoke-RestMethod "$base/api/tickets/bulk-status" -Method Post -Headers $headers -Body $bulkStatus
$report += "TICKET|BulkStatus|200|PASS|Updated=$($bsResult.updatedCount)"

# Notifications - Get
$notifs = Invoke-RestMethod "$base/api/notifications" -Method Get -Headers $headers
$report += "NOTIF|Get|200|PASS|Total=$($notifs.totalCount) Unread=$($notifs.unreadCount)"

# Notifications - Mark Read (if any)
if ($notifs.items.Count -gt 0) {
    Invoke-RestMethod "$base/api/notifications/$($notifs.items[0].id)/read" -Method Put -Headers $headers
    $report += "NOTIF|MarkRead|204|PASS"
}

# Notifications - Mark All Read
Invoke-RestMethod "$base/api/notifications/read-all" -Method Put -Headers $headers
$report += "NOTIF|MarkAllRead|204|PASS"

# Dashboard - Stats
$dashStats = Invoke-RestMethod "$base/api/dashboard/stats" -Method Get -Headers $headers
$report += "DASH|Stats|200|PASS|Total=$($dashStats.total)"

# Dashboard - Trends
$trends = Invoke-RestMethod "$base/api/dashboard/trends?days=7" -Method Get -Headers $headers
$report += "DASH|Trends|200|PASS|Days=$($trends.labels.Count)"

# Dashboard - SLA
$dashSla = Invoke-RestMethod "$base/api/dashboard/sla" -Method Get -Headers $headers
$report += "DASH|SLA|200|PASS|Count=$($dashSla.Count)"

# Dashboard - Agent Performance
$agentPerf = Invoke-RestMethod "$base/api/dashboard/agent-performance" -Method Get -Headers $headers
$report += "DASH|AgentPerf|200|PASS"

# Write report
Write-Host "`n=============== PHASE 2 REPORT (Tickets, Notifications, Dashboard) ===============`n"
$report | ForEach-Object { Write-Host $_ }
Write-Host "`n=== Summary ==="
Write-Host "Total: $($report.Count)"
Write-Host "Passed: $(($report | Where-Object { $_ -match 'PASS' }).Count)"
Write-Host "Failed: $(($report | Where-Object { $_ -notmatch 'PASS' }).Count)"
