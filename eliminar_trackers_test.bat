@echo off
chcp 65001 > nul
echo ========================================
echo   Eliminar Trackers de Prueba
echo ========================================
echo.

echo Eliminando trackers TEST_001, TEST_002, TEST_FULL, CONCURRENT_TEST_*...
echo.

psql -U postgres -d CattleTrackingDB -f eliminar_trackers_test.sql

echo.
echo ========================================
echo   Proceso completado
echo ========================================
pause
