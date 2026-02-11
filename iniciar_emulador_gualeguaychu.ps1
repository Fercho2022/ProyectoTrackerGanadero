# Script para iniciar emuladores GPS de Gualeguaychú
# Entre Ríos, Argentina

Write-Host "=" -NoNewline -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  EMULADORES GPS - GUALEGUAYCHU, ENTRE RIOS" -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

Write-Host "`n📍 Ubicación: Gualeguaychú, Entre Ríos, Argentina" -ForegroundColor White
Write-Host "📡 15 trackers GPS por emulador" -ForegroundColor White
Write-Host "🌐 API: http://localhost:5192/api/gps/update`n" -ForegroundColor White

Write-Host "Selecciona el emulador a ejecutar:`n" -ForegroundColor Green

Write-Host "  [1] 🐄 Emulador BÁSICO (15 trackers, visualización simple)" -ForegroundColor Cyan
Write-Host "      - Interfaz limpia y simple" -ForegroundColor Gray
Write-Host "      - Datos GPS completos" -ForegroundColor Gray
Write-Host "      - Ideal para pruebas rápidas`n" -ForegroundColor Gray

Write-Host "  [2] 📊 Emulador AVANZADO (15 trackers, monitoreo completo)" -ForegroundColor Magenta
Write-Host "      - Simulación de estados (pastando, descansando, alerta)" -ForegroundColor Gray
Write-Host "      - Estadísticas detalladas por tracker y campo" -ForegroundColor Gray
Write-Host "      - 3 campos: La Esperanza, San Jorge, El Ombú" -ForegroundColor Gray
Write-Host "      - Cambios de estado dinámicos`n" -ForegroundColor Gray

Write-Host "  [3] ⚙️  Ver configuración de trackers" -ForegroundColor Yellow
Write-Host "  [4] 🚪 Salir`n" -ForegroundColor Red

$opcion = Read-Host "Ingresa tu opción (1-4)"

switch ($opcion) {
    "1" {
        Write-Host "`n🚀 Iniciando Emulador GPS BÁSICO..." -ForegroundColor Green
        Write-Host "Presiona Ctrl+C para detener el emulador`n" -ForegroundColor Yellow
        Start-Sleep -Seconds 2
        python emulador_gps_gualeguaychu_basico.py
    }
    "2" {
        Write-Host "`n🚀 Iniciando Emulador GPS AVANZADO..." -ForegroundColor Green
        Write-Host "Presiona Ctrl+C para detener el emulador`n" -ForegroundColor Yellow
        Start-Sleep -Seconds 2
        python emulador_gps_gualeguaychu_avanzado.py
    }
    "3" {
        Write-Host "`n📋 CONFIGURACIÓN DE TRACKERS:" -ForegroundColor Cyan
        Write-Host "=" -NoNewline -ForegroundColor Cyan
        Write-Host "==========================================================" -ForegroundColor Cyan

        Write-Host "`n🏞️  CAMPO LA ESPERANZA (Oeste):" -ForegroundColor Yellow
        Write-Host "  COW_GPS_ER_01 - Vaca Manchada    | Pos: (-33.0156, -58.5523) | TT-4G-PRO" -ForegroundColor White
        Write-Host "  COW_GPS_ER_02 - Vaca Castaña     | Pos: (-33.0166, -58.5533) | TT-4G-PRO" -ForegroundColor White
        Write-Host "  COW_GPS_ER_03 - Vaca Negra       | Pos: (-33.0146, -58.5513) | TT-4G-PRO" -ForegroundColor White
        Write-Host "  COW_GPS_ER_04 - Vaca Blanca      | Pos: (-33.0176, -58.5543) | TT-4G-LITE" -ForegroundColor White
        Write-Host "  COW_GPS_ER_05 - Vaca Colorada    | Pos: (-33.0136, -58.5503) | TT-4G-PRO" -ForegroundColor White

        Write-Host "`n🏞️  CAMPO SAN JORGE (Noroeste):" -ForegroundColor Yellow
        Write-Host "  COW_GPS_ER_06 - Toro Hornero     | Pos: (-32.9876, -58.5423) | TT-4G-PRO" -ForegroundColor White
        Write-Host "  COW_GPS_ER_07 - Vaca Moteada     | Pos: (-32.9886, -58.5433) | TT-4G-PRO" -ForegroundColor White
        Write-Host "  COW_GPS_ER_08 - Vaca Pampa       | Pos: (-32.9866, -58.5413) | TT-4G-LITE" -ForegroundColor White
        Write-Host "  COW_GPS_ER_09 - Vaca Overa       | Pos: (-32.9896, -58.5443) | TT-4G-PRO" -ForegroundColor White
        Write-Host "  COW_GPS_ER_10 - Vaca Rubia       | Pos: (-32.9856, -58.5403) | TT-4G-PRO" -ForegroundColor White

        Write-Host "`n🏞️  CAMPO EL OMBÚ (Norte):" -ForegroundColor Yellow
        Write-Host "  COW_GPS_ER_11 - Vaca Bonita      | Pos: (-32.9756, -58.5123) | TT-4G-PRO" -ForegroundColor White
        Write-Host "  COW_GPS_ER_12 - Vaca Prieta      | Pos: (-32.9766, -58.5133) | TT-4G-PRO" -ForegroundColor White
        Write-Host "  COW_GPS_ER_13 - Vaca Zaino       | Pos: (-32.9746, -58.5113) | TT-4G-LITE" -ForegroundColor White
        Write-Host "  COW_GPS_ER_14 - Vaca Gateada     | Pos: (-32.9776, -58.5143) | TT-4G-PRO" -ForegroundColor White
        Write-Host "  COW_GPS_ER_15 - Vaca Rosada      | Pos: (-32.9736, -58.5103) | TT-4G-PRO" -ForegroundColor White

        Write-Host "`n📡 CARACTERÍSTICAS TÉCNICAS:" -ForegroundColor Cyan
        Write-Host "  • Modelos: TT-4G-PRO (panel solar) y TT-4G-LITE (solo batería)" -ForegroundColor Gray
        Write-Host "  • GPS: Quectel L86 / L76" -ForegroundColor Gray
        Write-Host "  • Módem: Quectel EC25 / EC21" -ForegroundColor Gray
        Write-Host "  • Operadores: Movistar AR y Claro AR" -ForegroundColor Gray
        Write-Host "  • Batería: 4000-5000 mAh" -ForegroundColor Gray
        Write-Host "  • Intervalo: 10 segundos" -ForegroundColor Gray

        Write-Host "`n" -NoNewline
        Read-Host "Presiona Enter para volver al menú"
        & $MyInvocation.MyCommand.Path
    }
    "4" {
        Write-Host "`n👋 Saliendo..." -ForegroundColor Yellow
        Start-Sleep -Seconds 1
        exit
    }
    default {
        Write-Host "`n❌ Opción inválida. Intenta de nuevo.`n" -ForegroundColor Red
        Start-Sleep -Seconds 2
        & $MyInvocation.MyCommand.Path
    }
}
