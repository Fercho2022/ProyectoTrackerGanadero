# Script de diagnóstico para verificar el estado del tracker COW_GPS_ER_01

Write-Host "=== DIAGNÓSTICO DEL TRACKER COW_GPS_ER_01 ===" -ForegroundColor Cyan
Write-Host ""

# 1. Ver estadísticas generales
Write-Host "1. Estadísticas generales de trackers:" -ForegroundColor Yellow
$stats = Invoke-RestMethod -Uri "http://127.0.0.1:5192/api/gps/stats" -Method GET
Write-Host "   Total trackers: $($stats.stats.totalTrackers)"
Write-Host "   Online: $($stats.stats.onlineTrackers)"
Write-Host "   Active: $($stats.stats.activeTrackers)"
Write-Host "   Discovered: $($stats.stats.discoveredTrackers)"
Write-Host ""

# 2. Mostrar consulta SQL para diagnóstico
Write-Host "2. Para ver el estado completo del tracker, ejecuta esta consulta SQL:" -ForegroundColor Yellow
Write-Host ""
Write-Host 'SELECT t."Id", t."DeviceId", t."Status", t."IsOnline", a."Id" as animal_id, a."Name" as animal_name' -ForegroundColor White
Write-Host 'FROM "Trackers" t LEFT JOIN "Animals" a ON a."TrackerId" = t."Id"' -ForegroundColor White
Write-Host 'WHERE t."DeviceId" = ' -NoNewline -ForegroundColor White
Write-Host "'COW_GPS_ER_01';" -ForegroundColor White
Write-Host ""
Write-Host "=== FIN DEL DIAGNÓSTICO ===" -ForegroundColor Cyan
