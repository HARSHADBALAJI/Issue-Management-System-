function Get-AuthHeaders {
  $body = @{ email = "admin@ticketingsystem.com"; password = "Admin@123" } | ConvertTo-Json
  $r = Invoke-RestMethod -Uri "http://localhost:5001/api/auth/login" -Method Post -Body $body -ContentType "application/json"
  return @{ Authorization = "Bearer $($r.accessToken)" }
}

function Get-Json($url, $headers) {
  $resp = Invoke-WebRequest -Uri $url -Method Get -Headers $headers -UseBasicParsing
  return $resp.Content | ConvertFrom-Json
}

function Post-Json($url, $body, $headers) {
  $jsonBody = $body | ConvertTo-Json -Depth 10
  return Invoke-WebRequest -Uri $url -Method Post -Body $jsonBody -ContentType "application/json" -Headers $headers -UseBasicParsing
}

function Put-Json($url, $body, $headers) {
  $jsonBody = $body | ConvertTo-Json -Depth 10
  return Invoke-WebRequest -Uri $url -Method Put -Body $jsonBody -ContentType "application/json" -Headers $headers -UseBasicParsing
}

$script:headers = Get-AuthHeaders
Write-Host "Authenticated. Headers ready." -ForegroundColor Green
