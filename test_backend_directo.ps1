# Script para probar que el backend tiene los datos correctos

Write-Host "=== PRUEBA DE BACKEND DIRECTO (SIN AUTENTICACION) ===" -ForegroundColor Cyan
Write-Host ""

# Customer ID 1 = fernandoCrosio
$response = Invoke-RestMethod -Uri "http://127.0.0.1:5192/api/setup/debug-locations/1" -Method GET

Write-Host "Respuesta del backend:" -ForegroundColor Green
$response | ConvertTo-Json -Depth 10
Write-Host ""
Write-Host "=== ANALISIS ===" -ForegroundColor Yellow
Write-Host "Total animales: $($response.animalCount)"
Write-Host ""

if ($response.animals) {
    foreach ($animal in $response.animals) {
        Write-Host "Animal: $($animal.name)" -ForegroundColor Green
        Write-Host "   Estado: $($animal.status)"
        Write-Host "   HasSignal: $($animal.hasSignal)"
        if ($animal.currentLocation) {
            Write-Host "   Ubicacion: $($animal.currentLocation.latitude), $($animal.currentLocation.longitude)" -ForegroundColor Cyan
            Write-Host "   Ultima actualizacion: $($animal.currentLocation.timestamp)"
        } else {
            Write-Host "   Sin ubicacion actual" -ForegroundColor Red
        }
        Write-Host ""
    }
}
