# Test 1: Is the API running?
Write-Host "=== Test 1: API reachable? ==="
try {
    $r = Invoke-WebRequest -Uri 'http://localhost:5192/swagger/index.html' -UseBasicParsing -TimeoutSec 5
    Write-Host "API Status: $($r.StatusCode) - API is running"
} catch {
    Write-Host "API NOT reachable: $($_.Exception.Message)"
}

# Test 2: CORS preflight for http://localhost:5280
Write-Host "`n=== Test 2: CORS preflight ==="
try {
    $headers = @{
        "Origin" = "http://localhost:5280"
        "Access-Control-Request-Method" = "POST"
        "Access-Control-Request-Headers" = "content-type"
    }
    $r2 = Invoke-WebRequest -Uri 'http://localhost:5192/api/users/login' -Method OPTIONS -Headers $headers -UseBasicParsing -TimeoutSec 5
    Write-Host "Preflight Status: $($r2.StatusCode)"
    Write-Host "Access-Control-Allow-Origin: $($r2.Headers['Access-Control-Allow-Origin'])"
    Write-Host "Access-Control-Allow-Methods: $($r2.Headers['Access-Control-Allow-Methods'])"
} catch {
    Write-Host "Preflight FAILED: $($_.Exception.Message)"
}

# Test 3: Direct POST login test
Write-Host "`n=== Test 3: Direct login POST ==="
try {
    $body = '{"username":"crosio.fernando@gmail.com","password":"test123"}'
    $r3 = Invoke-WebRequest -Uri 'http://localhost:5192/api/users/login' -Method POST -Body $body -ContentType 'application/json' -UseBasicParsing -TimeoutSec 5
    Write-Host "Login Status: $($r3.StatusCode)"
    Write-Host "Response: $($r3.Content.Substring(0, [Math]::Min(200, $r3.Content.Length)))"
} catch {
    $status = $_.Exception.Response.StatusCode
    Write-Host "Login Status: $status"
    Write-Host "Error: $($_.Exception.Message)"
}
