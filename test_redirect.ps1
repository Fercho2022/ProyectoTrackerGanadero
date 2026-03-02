# Test if the API redirects HTTP to HTTPS
# Using -MaximumRedirection 0 to NOT follow redirects
Write-Host "=== Test: Does API redirect HTTP to HTTPS? ==="
try {
    $r = Invoke-WebRequest -Uri 'http://localhost:5192/api/users/login' -Method POST -Body '{"username":"test","password":"test"}' -ContentType 'application/json' -UseBasicParsing -MaximumRedirection 0 -TimeoutSec 5
    Write-Host "Status: $($r.StatusCode) - No redirect"
} catch {
    $response = $_.Exception.Response
    if ($response -ne $null) {
        Write-Host "Status: $($response.StatusCode)"
        $location = $response.Headers.Location
        Write-Host "Location header: $location"
        if ($location -ne $null -and $location.ToString().StartsWith("https")) {
            Write-Host "*** API IS REDIRECTING HTTP TO HTTPS! This is the problem ***"
        }
    } else {
        Write-Host "Error: $($_.Exception.Message)"
    }
}
