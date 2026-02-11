$response = Invoke-RestMethod -Uri "http://127.0.0.1:5192/api/setup/fix-tracker-status" -Method POST -ContentType "application/json"
$response | ConvertTo-Json -Depth 10
Write-Host ""
Write-Host "Trackers arreglados:" $response.fixedCount
