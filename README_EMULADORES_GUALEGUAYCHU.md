# 🐄 Emuladores GPS Tracker Ganadero - Gualeguaychú, Entre Ríos

Sistema de emulación de trackers GPS para ganado vacuno en campos de Gualeguaychú, Entre Ríos, Argentina.

## 📍 Ubicación

- **Ciudad**: Gualeguaychú, Entre Ríos, Argentina
- **Coordenadas base**: -33.0096, -58.5173
- **Zona**: Área rural ganadera al oeste y norte de la ciudad

## 📡 Trackers Configurados

### 15 Dispositivos GPS Distribuidos en 3 Campos

#### 🏞️ Campo La Esperanza (Oeste) - 5 trackers
- **COW_GPS_ER_01** - Vaca Manchada | (-33.0156, -58.5523) | TT-4G-PRO ☀️
- **COW_GPS_ER_02** - Vaca Castaña | (-33.0166, -58.5533) | TT-4G-PRO ☀️
- **COW_GPS_ER_03** - Vaca Negra | (-33.0146, -58.5513) | TT-4G-PRO ☀️
- **COW_GPS_ER_04** - Vaca Blanca | (-33.0176, -58.5543) | TT-4G-LITE 🔋
- **COW_GPS_ER_05** - Vaca Colorada | (-33.0136, -58.5503) | TT-4G-PRO ☀️

#### 🏞️ Campo San Jorge (Noroeste) - 5 trackers
- **COW_GPS_ER_06** - Toro Hornero | (-32.9876, -58.5423) | TT-4G-PRO ☀️
- **COW_GPS_ER_07** - Vaca Moteada | (-32.9886, -58.5433) | TT-4G-PRO ☀️
- **COW_GPS_ER_08** - Vaca Pampa | (-32.9866, -58.5413) | TT-4G-LITE 🔋
- **COW_GPS_ER_09** - Vaca Overa | (-32.9896, -58.5443) | TT-4G-PRO ☀️
- **COW_GPS_ER_10** - Vaca Rubia | (-32.9856, -58.5403) | TT-4G-PRO ☀️

#### 🏞️ Campo El Ombú (Norte) - 5 trackers
- **COW_GPS_ER_11** - Vaca Bonita | (-32.9756, -58.5123) | TT-4G-PRO ☀️
- **COW_GPS_ER_12** - Vaca Prieta | (-32.9766, -58.5133) | TT-4G-PRO ☀️
- **COW_GPS_ER_13** - Vaca Zaino | (-32.9746, -58.5113) | TT-4G-LITE 🔋
- **COW_GPS_ER_14** - Vaca Gateada | (-32.9776, -58.5143) | TT-4G-PRO ☀️
- **COW_GPS_ER_15** - Vaca Rosada | (-32.9736, -58.5103) | TT-4G-PRO ☀️

## 🔧 Especificaciones Técnicas

### Hardware
- **Fabricante**: TechTrack Argentina
- **Modelos**:
  - TT-4G-PRO: Panel solar + batería 5000mAh
  - TT-4G-LITE: Solo batería 4000mAh
- **Módulo GPS**: Quectel L86 / L76
- **Módulo Módem**: Quectel EC25 / EC21 (4G LTE)
- **Operadores**: Movistar AR y Claro AR

### Datos Transmitidos
- **GPS**: Latitud, Longitud, Altitud, Velocidad, Rumbo
- **Calidad GPS**: HDOP, Precisión, Satélites visibles (7-12)
- **Batería**: Nivel (%), Voltaje, Temperatura, Estado de carga
- **Red Celular**: Potencia de señal, MCC, MNC, LAC, Cell ID
- **Sensores**: Acelerómetro (X, Y, Z), Temperatura interna
- **Configuración**: Serial, Firmware, Hardware, Intervalo

## 📦 Archivos del Proyecto

### Emuladores
1. **emulador_gps_gualeguaychu_basico.py**
   - Emulador simple con visualización limpia
   - 15 trackers con datos GPS completos
   - Ideal para pruebas rápidas

2. **emulador_gps_gualeguaychu_avanzado.py**
   - Simulación avanzada con estados dinámicos
   - Estadísticas detalladas por tracker y campo
   - Cambios de comportamiento automáticos
   - Monitoreo completo de telemetría

### Scripts de Inicio
- **iniciar_emulador_gualeguaychu.ps1** - Menú interactivo PowerShell

## 🚀 Uso

### Opción 1: Script PowerShell (Recomendado)
```powershell
.\iniciar_emulador_gualeguaychu.ps1
```

### Opción 2: Python directo

**Emulador Básico:**
```bash
python emulador_gps_gualeguaychu_basico.py
```

**Emulador Avanzado:**
```bash
python emulador_gps_gualeguaychu_avanzado.py
```

### Detener el Emulador
Presiona `Ctrl+C` para detener cualquier emulador de forma segura.

## 📊 Emulador BÁSICO

### Características
- ✅ Interfaz simple y clara
- ✅ 15 trackers GPS simultáneos
- ✅ Datos completos de electrónica
- ✅ Movimiento aleatorio realista
- ✅ Gestión de batería y carga solar
- ✅ Actualización cada 10 segundos

### Visualización
```
✅ [15:45:23] COW_GPS_ER_01 | GPS: (-33.015612, -58.552314) | Vel: 3.2 km/h | Sat: 9 | Señal: -72 dBm | Bat: 92.3%
✅ [15:45:24] COW_GPS_ER_02 | GPS: (-33.016701, -58.553401) | Vel: 2.8 km/h | Sat: 11 | Señal: -68 dBm | Bat: 88.1%
```

## 📈 Emulador AVANZADO

### Características
- ✅ Todo lo del emulador básico +
- ✅ Simulación de estados de comportamiento
- ✅ Estadísticas globales y por tracker
- ✅ Estadísticas por campo ganadero
- ✅ Cambios de estado automáticos
- ✅ Visualización mejorada con emojis
- ✅ Informe cada 5 ciclos

### Estados de Comportamiento
- 🌾 **Pastando** (60% probabilidad) - Movimiento normal
- 😴 **Descansando** (25%) - Movimiento mínimo
- 🚶 **Caminando** (10%) - Movimiento rápido
- ⚠️ **Alerta** (5%) - Movimiento muy rápido

### Visualización
```
⏰ CICLO #12 - 2026-01-30 15:45:23
================================================================================
  ✅ COW_GPS_ER_01 | La Esperanza  | GPS: (-33.015612, -58.552314) | Vel:  3.2 | Sat:  9 | Bat:  92.3% | 🌾 pastando
  ✅ COW_GPS_ER_06 | San Jorge     | GPS: (-32.987634, -58.542301) | Vel:  0.1 | Sat: 11 | Bat:  91.0% | 😴 descansando

🔄 Cambios de estado:
   ⚠️ COW_GPS_ER_08 → Estado: ALERTA

📊 ESTADÍSTICAS GLOBALES | ⏱️ Tiempo: 2m 15s | 🔄 Ciclos: 12 | 📤 Enviados: 180 | ✅ Exitosos: 178 | ❌ Errores: 2 | 📈 Tasa éxito: 98.9%

📍 ESTADÍSTICAS POR CAMPO:
   🏞️ La Esperanza        | Enviados:   60 | Exitosos:   60 | Tasa:  100.0%
   🏞️ San Jorge           | Enviados:   60 | Exitosos:   59 | Tasa:   98.3%
   🏞️ El Ombú             | Enviados:   60 | Exitosos:   59 | Tasa:   98.3%
```

## 🔗 Configuración de API

Por defecto, los emuladores se conectan a:
- **URL**: `http://localhost:5192/api/gps/update`
- **Intervalo**: 10 segundos
- **Método**: POST con JSON

### Modificar la URL de la API

Edita la constante `API_URL` en el archivo Python:
```python
API_URL = "http://localhost:5192/api/gps/update"
```

### Modificar el Intervalo

Edita la constante `INTERVALO_ACTUALIZACION`:
```python
INTERVALO_ACTUALIZACION = 10  # segundos
```

## 📋 Requisitos

### Software Necesario
- Python 3.8 o superior
- Librería `requests`

### Instalar Dependencias
```bash
pip install requests
```

### API Backend
El backend .NET debe estar corriendo en:
```
http://localhost:5192
```

Verifica que el endpoint `/api/gps/update` esté disponible.

## 🔍 Datos de Ejemplo

### Payload JSON Enviado
```json
{
  "deviceId": "COW_GPS_ER_01",
  "serialNumber": "GPS-ER-2024-001",
  "manufacturer": "TechTrack Argentina",
  "model": "TT-4G-PRO",
  "firmwareVersion": "2.4.1",
  "latitude": -33.015612,
  "longitude": -58.552314,
  "timestamp": "2026-01-30T15:45:23.123456Z",
  "altitude": 25.3,
  "speed": 3.2,
  "heading": 145.7,
  "satellites": 9,
  "hdop": 1.2,
  "accuracy": 5.8,
  "batteryLevel": 92.3,
  "batteryVoltage": 4.16,
  "chargingStatus": "solar",
  "signalStrength": -72,
  "networkType": "4G LTE",
  "operator": "Movistar AR",
  "internalTemperature": 32.4,
  "gpsModule": "Quectel L86",
  "modemModule": "Quectel EC25"
}
```

## 🐛 Solución de Problemas

### Error de Conexión
```
⚠️ COW_GPS_ER_01 | Error de conexión: Connection refused
```
**Solución**: Verifica que la API esté corriendo en el puerto 5192.

### Timeout
```
⏱️ COW_GPS_ER_02 | Timeout al conectar con la API
```
**Solución**: La API está tardando mucho. Verifica la base de datos y logs del servidor.

### Error 400 Bad Request
**Solución**: El formato del payload no es correcto. Revisa los logs del backend.

### Error 500 Internal Server Error
**Solución**: Error en el servidor. Revisa los logs del backend API.

## 📝 Notas Técnicas

### Movimiento Simulado
- Los animales se mueven dentro de un área de ~200-300 metros
- El movimiento es aleatorio pero realista (velocidades de 0-5 km/h)
- Hay pausas ocasionales que simulan descanso

### Batería
- Los trackers con panel solar se cargan durante el día (9:00-18:00)
- La batería se descarga gradualmente cuando no hay sol
- Nivel de batería: 0-100%

### Calidad GPS
- Satélites visibles: 7-12 (típico en campo abierto)
- HDOP: 0.8-2.5 (buena calidad)
- Precisión: 3-12 metros

### Red Celular
- RSSI: -85 a -55 dBm (señal típica en zona rural)
- Operadores reales: Movistar AR y Claro AR
- Tecnología: 4G LTE

## 📞 Soporte

Para problemas o consultas sobre los emuladores, revisa:
1. Logs de la consola del emulador
2. Logs del backend API
3. Estado de la base de datos PostgreSQL

## 📄 Licencia

Estos emuladores son parte del proyecto TrackerGanadero y están diseñados exclusivamente para desarrollo y pruebas.

---

**Última actualización**: 30 de enero de 2026
**Versión**: 1.0
**Autor**: Sistema Tracker Ganadero
