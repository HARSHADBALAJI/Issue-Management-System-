# ============================================================
# SEED SCRIPT - Issue Management System
# Creates: Applications, Users, and User-Application mappings
# Run:  powershell -ExecutionPolicy Bypass -File scripts\seed-data.ps1
# ============================================================

$ErrorActionPreference = "Stop"
$BASE = "http://localhost:5001/api"

# --- LOGIN ---
Write-Host ""
Write-Host "[1/4] Logging in..." -ForegroundColor Cyan
$login = Invoke-RestMethod -Uri "$BASE/auth/login" -Method POST -ContentType "application/json" -Body '{"email":"admin@ticketingsystem.com","password":"Admin@123"}'
$token = $login.accessToken
$h = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }
Write-Host "  OK" -ForegroundColor Green

# ============================================================
# APPLICATIONS
# ============================================================
Write-Host ""
Write-Host "[2/4] Creating applications..." -ForegroundColor Cyan

$appNames = @(
    "IB4U"
    "Material NxT"
    "Fabrication application"
    "Quality Audit Module"
    "PCMS Application"
    "Precasting Application - MABP"
    "Precasting Application - MPSB"
    "Earthwork material monitoring - MPRRP"
    "Disposal Monitoring App"
    "Erection Application"
    "Digital Engineers Digest"
    "QMS"
    "HSD Application"
    "Quality Labs"
    "Quality WMS Module"
    "Quality Inspection Module"
    "Task Karo"
    "ESG Module"
    "Automated BOQ generation for tender projects"
    "Integrated Ticketing System"
    "Material NxT Trend Analysis"
    "QMS Migration to React-Node.js"
    "Vision Analytics - MPSB"
    "Optrix 1"
    "Optrix 2"
    "Quality Predictive Analysis (Phase 1)"
    "AGL Automation (Phase 1)"
    "AGL Automation (Phase 2)"
    "MEP Automation (Phase 1)"
    "MEP Automation (Phase 2)"
    "Quality Lab"
    "Finance"
    "QMC DPR"
    "Monthly Report Automation"
    "Quality Image Analytics"
    "Quality Risk Profile"
    "O and M - Azure Data Factory"
    "HSD Budget Prediction"
    "Vision Analytics - MPSB - Safety Module"
    "Vision Analytics - UAUC Recommendation Module"
    "Vision Analytics - MVDP/MDLRP"
    "Integrated Vision Analytics Website"
    "OCR - Setting Out Quantities"
    "Project Control Chat Bot for Historical Project Data"
    "Transportation Problem - Optimized Route for Concrete Delivery"
    "Job Profile HR"
    "Incident Prediction"
    "Vision Analytics - MPSB - Precast Activities Module"
    "Concrete Reconciliation Module"
    "Quality Skill Mapping Module - Q-GRID"
    "4D BIM"
    "BIM to GIS"
    "SHEILD"
    "Lessons Learned App"
    "Guest House Application - Stay Comfort"
    "Realtime Formwork Monitoring"
    "EHS Performance - 10 Points"
    "Quality Monthly Report"
    "Safety Performance"
    "Asset One Trend Analysis"
    "Incident Investigation Module"
    "SHEILD Trend Analysis"
    "P4xy Inspection Report"
    "P4xy Dispatch Material Tracking"
    "EOT Development"
    "My Story Application"
    "Tender Application"
    "Fuel Sensor Through Debit Note"
    "Minor Asset Tracking"
    "EOT Phase 2"
    "Claims Module"
    "MATTRACK - Borrow Area Material Tracking"
    "TAB Material Tracking"
    "Integrated CACM Platform"
)

$appIds = @{}
foreach ($name in $appNames) {
    $body = @{ Name = $name; IsActive = $true } | ConvertTo-Json
    try {
        $res = Invoke-RestMethod -Uri "$BASE/applications" -Method POST -Headers $h -Body $body
        $appIds[$name] = $res.id
        Write-Host "  + $name" -ForegroundColor DarkGray
    } catch {
        $existing = Invoke-RestMethod -Uri "$BASE/applications?pageSize=200" -Headers $h
        $found = $existing.items | Where-Object { $_.name -eq $name }
        if ($found) { $appIds[$name] = $found.id; Write-Host "  = $name (exists)" -ForegroundColor Yellow }
        else { Write-Host "  ! FAILED: $name" -ForegroundColor Red }
    }
}
Write-Host "  Total: $($appIds.Count) applications" -ForegroundColor Green

# ============================================================
# USERS
# ============================================================
Write-Host ""
Write-Host "[3/4] Creating users..." -ForegroundColor Cyan

# Akanksha
$akankshaApps = @(
    "IB4U"
    "Material NxT"
    "Fabrication application"
    "Quality Audit Module"
    "PCMS Application"
    "Precasting Application - MABP"
    "Precasting Application - MPSB"
    "Earthwork material monitoring - MPRRP"
    "Disposal Monitoring App"
    "Erection Application"
    "Digital Engineers Digest"
    "QMS"
    "HSD Application"
    "Quality Labs"
    "Quality WMS Module"
    "Quality Inspection Module"
    "Task Karo"
    "ESG Module"
    "Automated BOQ generation for tender projects"
    "Integrated Ticketing System"
    "Material NxT Trend Analysis"
    "QMS Migration to React-Node.js"
)

# Deepshikha
$deepshikhaApps = @(
    "Vision Analytics - MPSB"
    "Optrix 1"
    "Optrix 2"
    "Quality Predictive Analysis (Phase 1)"
    "AGL Automation (Phase 1)"
    "AGL Automation (Phase 2)"
    "MEP Automation (Phase 1)"
    "MEP Automation (Phase 2)"
    "Quality Lab"
    "Finance"
    "QMC DPR"
    "Monthly Report Automation"
    "Quality Image Analytics"
    "Quality Risk Profile"
    "O and M - Azure Data Factory"
    "HSD Budget Prediction"
    "Vision Analytics - MPSB - Safety Module"
    "Vision Analytics - UAUC Recommendation Module"
    "Vision Analytics - MVDP/MDLRP"
    "Integrated Vision Analytics Website"
    "OCR - Setting Out Quantities"
    "Project Control Chat Bot for Historical Project Data"
    "Transportation Problem - Optimized Route for Concrete Delivery"
    "Job Profile HR"
    "Incident Prediction"
    "Automated BOQ generation for tender projects"
    "PCMS Application"
    "Vision Analytics - MPSB - Precast Activities Module"
)

# Jayasmita
$jayasmitaApps = @(
    "Concrete Reconciliation Module"
    "Integrated Ticketing System"
    "Task Karo"
    "Automated BOQ generation for tender projects"
    "Quality Skill Mapping Module - Q-GRID"
    "4D BIM"
    "BIM to GIS"
)

# Mounika
$mounikaApps = @(
    "SHEILD"
    "Lessons Learned App"
    "Guest House Application - Stay Comfort"
    "Realtime Formwork Monitoring"
    "EHS Performance - 10 Points"
    "Quality Monthly Report"
    "Safety Performance"
    "Asset One Trend Analysis"
    "Incident Investigation Module"
    "SHEILD Trend Analysis"
    "Optrix 1"
)

# Uday Kiran
$udayApps = @(
    "P4xy Inspection Report"
    "P4xy Dispatch Material Tracking"
    "EOT Development"
    "My Story Application"
    "Tender Application"
    "Fuel Sensor Through Debit Note"
    "Minor Asset Tracking"
    "EOT Phase 2"
    "Claims Module"
    "MATTRACK - Borrow Area Material Tracking"
    "TAB Material Tracking"
    "Integrated CACM Platform"
    "Precasting Application - MPSB"
)

function New-User($name, $email, $apps) {
    $body = @{ Name = $name; Email = $email; DepartmentName = "General"; Role = "spoc"; Status = "active" } | ConvertTo-Json
    try {
        $res = Invoke-RestMethod -Uri "$BASE/users" -Method POST -Headers $h -Body $body
        Write-Host "  + $name (id=$($res.id))" -ForegroundColor DarkGray
        return @{ id = $res.id; name = $name; email = $email; apps = $apps }
    } catch {
        $existing = Invoke-RestMethod -Uri "$BASE/users?pageSize=200" -Headers $h
        $found = $existing.items | Where-Object { $_.email -eq $email }
        if ($found) { Write-Host "  = $name (exists, id=$($found.id))" -ForegroundColor Yellow; return @{ id = $found.id; name = $name; email = $email; apps = $apps } }
        else { Write-Host "  ! FAILED: $name" -ForegroundColor Red; return $null }
    }
}

$allUsers = @()
$allUsers += New-User "Akanksha Ramesh Sutar" "akanksha.sutar@lntecc.com" $akankshaApps
$allUsers += New-User "Deepshikha Sen" "deepshikha.sen@lntecc.com" $deepshikhaApps
$allUsers += New-User "Jayasmita Rout" "jayasmitarout@lntecc.com" $jayasmitaApps
$allUsers += New-User "Chennaboina Sai Mounika" "chennaboina.mounika@lntecc.com" $mounikaApps
$allUsers += New-User "Lanke Uday Kiran" "lanke.kiran@lntecc.com" $udayApps

# ============================================================
# USER <-> APPLICATION MAPPINGS
# ============================================================
Write-Host ""
Write-Host "[4/4] Assigning users to applications..." -ForegroundColor Cyan

foreach ($u in $allUsers) {
    if (-not $u) { continue }
    $appIdsList = @()
    foreach ($appName in $u.apps) {
        $aid = $appIds[$appName]
        if ($aid) {
            $appIdsList += $aid
        } else {
            Write-Host "  ! App not found: $appName" -ForegroundColor Red
        }
    }
    if ($appIdsList.Count -eq 0) { continue }
    $body = @{ ApplicationIds = $appIdsList } | ConvertTo-Json
    try {
        Invoke-RestMethod -Uri "$BASE/users/$($u.id)/applications" -Method PUT -Headers $h -Body $body | Out-Null
        Write-Host "  + $($u.name) -> $($appIdsList.Count) apps" -ForegroundColor DarkGray
    } catch {
        Write-Host "  ! Failed: $($u.name) - $($_.Exception.Message)" -ForegroundColor Red
    }
}

# ============================================================
# VERIFY
# ============================================================
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " SEED COMPLETE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$allUsersRes = Invoke-RestMethod -Uri "$BASE/users?pageSize=100" -Headers $h
$allAppsRes  = Invoke-RestMethod -Uri "$BASE/applications?pageSize=200" -Headers $h

Write-Host ""
Write-Host "Users ($($allUsersRes.totalCount)):" -ForegroundColor Yellow
foreach ($u in $allUsersRes.items) {
    Write-Host "  $($u.id). $($u.name) | $($u.email) | $($u.role) | Apps: $($u.assignedApps.Count)"
}

Write-Host ""
Write-Host "Applications ($($allAppsRes.totalCount)):" -ForegroundColor Yellow
foreach ($a in $allAppsRes.items) {
    Write-Host "  $($a.id). $($a.name) (SPOCs: $($a.assignedUserCount))"
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
