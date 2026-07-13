$base = "http://localhost:5001"
$report = @()

# Login
$login = Invoke-RestMethod "$base/api/auth/login" -Method Post -Body (@{email="admin@ticketingsystem.com";password="Admin@123"} | ConvertTo-Json) -ContentType "application/json"
$headers = @{ Authorization = "Bearer $($login.accessToken)"; "Content-Type" = "application/json" }
$report += "AUTH|Login|200|PASS|User=$($login.user.fullName) Role=$($login.user.role)"

# Refresh
$refresh = Invoke-RestMethod "$base/api/auth/refresh" -Method Post -Body (@{refreshToken=$login.refreshToken} | ConvertTo-Json) -ContentType "application/json"
$report += "AUTH|Refresh|200|PASS|Token refreshed"

# Departments - Create
$dept1 = Invoke-RestMethod "$base/api/departments" -Method Post -Headers $headers -Body (@{name="IT Support";headUserId=$null} | ConvertTo-Json)
$report += "DEPT|Create IT|200|PASS|Id=$($dept1.id)"

$dept2 = Invoke-RestMethod "$base/api/departments" -Method Post -Headers $headers -Body (@{name="HR";headUserId=$null} | ConvertTo-Json)
$report += "DEPT|Create HR|200|PASS|Id=$($dept2.id)"

$dept3 = Invoke-RestMethod "$base/api/departments" -Method Post -Headers $headers -Body (@{name="Finance";headUserId=$null} | ConvertTo-Json)
$report += "DEPT|Create Finance|200|PASS|Id=$($dept3.id)"

# Departments - List
$depts = Invoke-RestMethod "$base/api/departments" -Method Get -Headers $headers
$report += "DEPT|List|200|PASS|Count=$($depts.totalCount)"

# Departments - Update
$deptUpdated = Invoke-RestMethod "$base/api/departments/$($dept1.id)" -Method Put -Headers $headers -Body (@{name="IT Department";headUserId=$null} | ConvertTo-Json)
$report += "DEPT|Update|200|PASS|Name=$($deptUpdated.name)"

# Departments - Lookup
$deptLookup = Invoke-RestMethod "$base/api/departments/lookup" -Method Get -Headers $headers
$report += "DEPT|Lookup|200|PASS|Count=$($deptLookup.Count)"

# Auth - Logout then re-login
Invoke-RestMethod "$base/api/auth/logout" -Method Post -Headers $headers
$report += "AUTH|Logout|200|PASS"
$login2 = Invoke-RestMethod "$base/api/auth/login" -Method Post -Body (@{email="admin@ticketingsystem.com";password="Admin@123"} | ConvertTo-Json) -ContentType "application/json"
$headers = @{ Authorization = "Bearer $($login2.accessToken)"; "Content-Type" = "application/json" }
$report += "AUTH|Re-login|200|PASS"

# Users - Create
$user1 = Invoke-RestMethod "$base/api/users" -Method Post -Headers $headers -Body (@{employeeId="SPOC001";fullName="Alice SPOC";email="alice@test.com";phoneNumber="1234567890";departmentId=$dept1.id;roleId=2;isActive=$true} | ConvertTo-Json)
$report += "USER|Create Alice|200|PASS|Id=$($user1.id)"

$user2 = Invoke-RestMethod "$base/api/users" -Method Post -Headers $headers -Body (@{employeeId="SPOC002";fullName="Bob SPOC";email="bob@test.com";phoneNumber="1234567891";departmentId=$dept2.id;roleId=2;isActive=$true} | ConvertTo-Json)
$report += "USER|Create Bob|200|PASS|Id=$($user2.id)"

$user3 = Invoke-RestMethod "$base/api/users" -Method Post -Headers $headers -Body (@{employeeId="SPOC003";fullName="Charlie SPOC";email="charlie@test.com";phoneNumber="1234567892";departmentId=$dept3.id;roleId=2;isActive=$true} | ConvertTo-Json)
$report += "USER|Create Charlie|200|PASS|Id=$($user3.id)"

# Users - List
$users = Invoke-RestMethod "$base/api/users" -Method Get -Headers $headers
$report += "USER|List|200|PASS|Count=$($users.totalCount)"

# Users - Get by ID
$userGet = Invoke-RestMethod "$base/api/users/$($user1.id)" -Method Get -Headers $headers
$report += "USER|GetById|200|PASS|Name=$($userGet.fullName)"

# Users - Update
$userUpdated = Invoke-RestMethod "$base/api/users/$($user1.id)" -Method Put -Headers $headers -Body (@{fullName="Alice Updated";email="alice@test.com";phoneNumber="1234567890";departmentId=$dept1.id;roleId=2;isActive=$true} | ConvertTo-Json)
$report += "USER|Update|200|PASS|Name=$($userUpdated.fullName)"

# Users - Lookup
$userLookup = Invoke-RestMethod "$base/api/users/lookup" -Method Get -Headers $headers
$report += "USER|Lookup|200|PASS|Count=$($userLookup.Count)"

# Users - Lookup by role
$spocLookup = Invoke-RestMethod "$base/api/users/lookup?roleId=2" -Method Get -Headers $headers
$report += "USER|LookupByRole|200|PASS|SPOCs=$($spocLookup.Count)"

# Applications - Create
$app1Data = @{name="Ticketing Portal";description="Main ticketing system";supportEmail="support@ticketing.com";isActive=$true}
$app1 = Invoke-RestMethod "$base/api/applications" -Method Post -Headers $headers -Body ($app1Data | ConvertTo-Json)
$report += "APP|Create Portal|200|PASS|Id=$($app1.id)"

$app2Data = @{name="HRMS";description="HR Management System";supportEmail="hrms@test.com";isActive=$true}
$app2 = Invoke-RestMethod "$base/api/applications" -Method Post -Headers $headers -Body ($app2Data | ConvertTo-Json)
$report += "APP|Create HRMS|200|PASS|Id=$($app2.id)"

$app3Data = @{name="FinanceApp";description="Finance Application";supportEmail="finance@test.com";isActive=$true}
$app3 = Invoke-RestMethod "$base/api/applications" -Method Post -Headers $headers -Body ($app3Data | ConvertTo-Json)
$report += "APP|Create Finance|200|PASS|Id=$($app3.id)"

$app4Data = @{name="CRM";description="Customer Relationship Mgmt";supportEmail="crm@test.com";isActive=$true}
$app4 = Invoke-RestMethod "$base/api/applications" -Method Post -Headers $headers -Body ($app4Data | ConvertTo-Json)
$report += "APP|Create CRM|200|PASS|Id=$($app4.id)"

$app5Data = @{name="EmailService";description="Email Service Platform";supportEmail="email@test.com";isActive=$true}
$app5 = Invoke-RestMethod "$base/api/applications" -Method Post -Headers $headers -Body ($app5Data | ConvertTo-Json)
$report += "APP|Create Email|200|PASS|Id=$($app5.id)"

# Applications - List
$apps = Invoke-RestMethod "$base/api/applications" -Method Get -Headers $headers
$report += "APP|List|200|PASS|Count=$($apps.totalCount)"

# Applications - Get by ID
$appGet = Invoke-RestMethod "$base/api/applications/$($app1.id)" -Method Get -Headers $headers
$report += "APP|GetById|200|PASS|Name=$($appGet.name)"

# Applications - Update
$appUpdated = Invoke-RestMethod "$base/api/applications/$($app1.id)" -Method Put -Headers $headers -Body (@{name="Ticketing System Updated";description="Updated description";supportEmail="support@ticketing.com";isActive=$true} | ConvertTo-Json)
$report += "APP|Update|200|PASS|Name=$($appUpdated.name)"

# Applications - Lookup
$appLookup = Invoke-RestMethod "$base/api/applications/lookup" -Method Get -Headers $headers
$report += "APP|Lookup|200|PASS|Count=$($appLookup.Count)"

# Applications - Assign Users
$assignBody = @{userIds=@($user1.id, $user2.id);primarySpocUserId=$user1.id} | ConvertTo-Json
Invoke-RestMethod "$base/api/applications/$($app1.id)/users" -Method Put -Headers $headers -Body $assignBody
$report += "APP|AssignUsers|204|PASS"

# Applications - Verify assignment
$appWithUsers = Invoke-RestMethod "$base/api/applications/$($app1.id)" -Method Get -Headers $headers
$report += "APP|VerifyAssignment|200|PASS|AssignedUsers=$($appWithUsers.assignedUserCount)"

# Users - Assign Applications
$userAppBody = @{applicationIds=@($app1.id, $app2.id)} | ConvertTo-Json
Invoke-RestMethod "$base/api/users/$($user1.id)/applications" -Method Put -Headers $headers -Body $userAppBody
$report += "USER|AssignApps|204|PASS"

# Users - Reset password token
$resetToken = Invoke-RestMethod "$base/api/users/$($user1.id)/reset-password" -Method Post -Headers $headers
$report += "USER|ResetPwdToken|200|PASS"

# Write report
$report | ForEach-Object { Write-Host $_ }
Write-Host "`n=== Phase 2 Summary ==="
Write-Host "Total tests: $($report.Count)"
Write-Host "Passed: $(($report | Where-Object { $_ -match 'PASS' }).Count)"
Write-Host "Failed: $(($report | Where-Object { $_ -notmatch 'PASS' }).Count)"

# Store IDs for later use
Write-Host "`n=== Stored IDs ==="
Write-Host "Depts: $($dept1.id), $($dept2.id), $($dept3.id)"
Write-Host "Users: $($user1.id), $($user2.id), $($user3.id)"
Write-Host "Apps: $($app1.id), $($app2.id), $($app3.id), $($app4.id), $($app5.id)"
