"""
Emulador GPS para Trackers Ganaderos - Gualeguaychú, Entre Ríos, Argentina
Simula dispositivos GPS reales con datos de electrónica completos
"""

import requests
import time
import random
from datetime import datetime, timezone
import json

# Configuración API
API_URL = "http://localhost:5192/api/gps/update"
INTERVALO_ACTUALIZACION = 10  # segundos

# Coordenadas de Gualeguaychú, Entre Ríos, Argentina
# Centro de zona ganadera: Área rural al oeste de la ciudad
GUALEGUAYCHU_BASE_LAT = -33.0096
GUALEGUAYCHU_BASE_LON = -58.5173

# Configuración de Trackers GPS Reales - 15 dispositivos
TRACKERS = [
    # Campo La Esperanza (zona oeste) - 5 trackers
    {
        "deviceId": "COW_GPS_ER_01",
        "serialNumber": "GPS-ER-2024-001",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -33.0156,
        "lon": -58.5523,
        "nombre": "Tracker Campo La Esperanza #1",
        "batteryCapacity": 5000,
        "batteryLevel": 92,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Movistar AR",
        "velocidad_max": 4.5,
        "area_movimiento": 0.003
    },
    {
        "deviceId": "COW_GPS_ER_02",
        "serialNumber": "GPS-ER-2024-002",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -33.0166,
        "lon": -58.5533,
        "nombre": "Tracker Campo La Esperanza #2",
        "batteryCapacity": 5000,
        "batteryLevel": 88,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Movistar AR",
        "velocidad_max": 3.8,
        "area_movimiento": 0.0025
    },
    {
        "deviceId": "COW_GPS_ER_03",
        "serialNumber": "GPS-ER-2024-003",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -33.0146,
        "lon": -58.5513,
        "nombre": "Tracker Campo La Esperanza #3",
        "batteryCapacity": 5000,
        "batteryLevel": 85,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Claro AR",
        "velocidad_max": 4.2,
        "area_movimiento": 0.0028
    },
    {
        "deviceId": "COW_GPS_ER_04",
        "serialNumber": "GPS-ER-2024-004",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-LITE",
        "firmwareVersion": "2.3.8",
        "hardwareVersion": "HW-3.1",
        "lat": -33.0176,
        "lon": -58.5543,
        "nombre": "Tracker Campo La Esperanza #4",
        "batteryCapacity": 4000,
        "batteryLevel": 78,
        "solarPanel": False,
        "transmissionPower": -12,
        "gpsModule": "Quectel L76",
        "modemModule": "Quectel EC21",
        "networkOperator": "Movistar AR",
        "velocidad_max": 4.0,
        "area_movimiento": 0.0032
    },
    {
        "deviceId": "COW_GPS_ER_05",
        "serialNumber": "GPS-ER-2024-005",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -33.0136,
        "lon": -58.5503,
        "nombre": "Tracker Campo La Esperanza #5",
        "batteryCapacity": 5000,
        "batteryLevel": 94,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Claro AR",
        "velocidad_max": 3.5,
        "area_movimiento": 0.0027
    },

    # Campo San Jorge (zona noroeste) - 5 trackers
    {
        "deviceId": "COW_GPS_ER_06",
        "serialNumber": "GPS-ER-2024-006",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -32.9876,
        "lon": -58.5423,
        "nombre": "Tracker Campo San Jorge #1",
        "batteryCapacity": 5000,
        "batteryLevel": 91,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Claro AR",
        "velocidad_max": 4.3,
        "area_movimiento": 0.003
    },
    {
        "deviceId": "COW_GPS_ER_07",
        "serialNumber": "GPS-ER-2024-007",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -32.9886,
        "lon": -58.5433,
        "nombre": "Tracker Campo San Jorge #2",
        "batteryCapacity": 5000,
        "batteryLevel": 87,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Movistar AR",
        "velocidad_max": 3.9,
        "area_movimiento": 0.0029
    },
    {
        "deviceId": "COW_GPS_ER_08",
        "serialNumber": "GPS-ER-2024-008",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-LITE",
        "firmwareVersion": "2.3.8",
        "hardwareVersion": "HW-3.1",
        "lat": -32.9866,
        "lon": -58.5413,
        "nombre": "Tracker Campo San Jorge #3",
        "batteryCapacity": 4000,
        "batteryLevel": 82,
        "solarPanel": False,
        "transmissionPower": -12,
        "gpsModule": "Quectel L76",
        "modemModule": "Quectel EC21",
        "networkOperator": "Claro AR",
        "velocidad_max": 4.1,
        "area_movimiento": 0.0031
    },
    {
        "deviceId": "COW_GPS_ER_09",
        "serialNumber": "GPS-ER-2024-009",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -32.9896,
        "lon": -58.5443,
        "nombre": "Tracker Campo San Jorge #4",
        "batteryCapacity": 5000,
        "batteryLevel": 89,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Movistar AR",
        "velocidad_max": 3.7,
        "area_movimiento": 0.0026
    },
    {
        "deviceId": "COW_GPS_ER_10",
        "serialNumber": "GPS-ER-2024-010",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -32.9856,
        "lon": -58.5403,
        "nombre": "Tracker Campo San Jorge #5",
        "batteryCapacity": 5000,
        "batteryLevel": 93,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Claro AR",
        "velocidad_max": 4.0,
        "area_movimiento": 0.0028
    },

    # Campo El Ombú (zona norte) - 5 trackers
    {
        "deviceId": "COW_GPS_ER_11",
        "serialNumber": "GPS-ER-2024-011",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -32.9756,
        "lon": -58.5123,
        "nombre": "Tracker Campo El Ombú #1",
        "batteryCapacity": 5000,
        "batteryLevel": 90,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Movistar AR",
        "velocidad_max": 4.4,
        "area_movimiento": 0.003
    },
    {
        "deviceId": "COW_GPS_ER_12",
        "serialNumber": "GPS-ER-2024-012",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -32.9766,
        "lon": -58.5133,
        "nombre": "Tracker Campo El Ombú #2",
        "batteryCapacity": 5000,
        "batteryLevel": 86,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Claro AR",
        "velocidad_max": 3.8,
        "area_movimiento": 0.0027
    },
    {
        "deviceId": "COW_GPS_ER_13",
        "serialNumber": "GPS-ER-2024-013",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-LITE",
        "firmwareVersion": "2.3.8",
        "hardwareVersion": "HW-3.1",
        "lat": -32.9746,
        "lon": -58.5113,
        "nombre": "Tracker Campo El Ombú #3",
        "batteryCapacity": 4000,
        "batteryLevel": 80,
        "solarPanel": False,
        "transmissionPower": -12,
        "gpsModule": "Quectel L76",
        "modemModule": "Quectel EC21",
        "networkOperator": "Movistar AR",
        "velocidad_max": 4.2,
        "area_movimiento": 0.0032
    },
    {
        "deviceId": "COW_GPS_ER_14",
        "serialNumber": "GPS-ER-2024-014",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -32.9776,
        "lon": -58.5143,
        "nombre": "Tracker Campo El Ombú #4",
        "batteryCapacity": 5000,
        "batteryLevel": 88,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Claro AR",
        "velocidad_max": 3.6,
        "area_movimiento": 0.0025
    },
    {
        "deviceId": "COW_GPS_ER_15",
        "serialNumber": "GPS-ER-2024-015",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO",
        "firmwareVersion": "2.4.1",
        "hardwareVersion": "HW-3.2",
        "lat": -32.9736,
        "lon": -58.5103,
        "nombre": "Tracker Campo El Ombú #5",
        "batteryCapacity": 5000,
        "batteryLevel": 95,
        "solarPanel": True,
        "transmissionPower": -10,
        "gpsModule": "Quectel L86",
        "modemModule": "Quectel EC25",
        "networkOperator": "Movistar AR",
        "velocidad_max": 4.1,
        "area_movimiento": 0.0029
    }
]

# Células de torres de telefonía cercanas (simuladas)
CELL_TOWERS = [
    {"mcc": 722, "mnc": 70, "lac": 1234, "cellid": 56781},  # Movistar
    {"mcc": 722, "mnc": 310, "lac": 1235, "cellid": 56782}, # Claro
]

def simular_movimiento_realista(tracker):
    """Simula movimiento realista de ganado en campo"""
    # Movimiento aleatorio dentro del área de pastoreo
    # Los animales se mueven lentamente, con pausas

    if random.random() < 0.3:  # 30% de probabilidad de estar quieto
        velocidad = 0
        lat_offset = 0
        lon_offset = 0
    else:
        # Movimiento lento típico de ganado pastando
        lat_offset = random.uniform(-tracker["area_movimiento"], tracker["area_movimiento"])
        lon_offset = random.uniform(-tracker["area_movimiento"], tracker["area_movimiento"])
        velocidad = random.uniform(0, tracker["velocidad_max"])

    # Actualizar posición
    tracker["lat"] += lat_offset
    tracker["lon"] += lon_offset

    return velocidad

def calcular_hdop():
    """Calcula HDOP (Horizontal Dilution of Precision) realista"""
    # HDOP típico: 0.5-2.0 = excelente, 2.0-5.0 = bueno, 5.0-10.0 = moderado
    return round(random.uniform(0.8, 2.5), 1)

def calcular_signal_strength():
    """Calcula potencia de señal celular realista (RSSI)"""
    # RSSI en dBm: -50 = excelente, -70 = bueno, -90 = pobre, -110 = muy pobre
    return random.randint(-85, -55)

def calcular_satelites_visibles():
    """Número de satélites GPS visibles"""
    # Típicamente 6-12 satélites en campo abierto
    return random.randint(7, 12)

def simular_temperatura_interna():
    """Simula temperatura interna del dispositivo"""
    # Temperatura ambiente en Entre Ríos: 10-35°C
    # + calentamiento interno del dispositivo: +5-10°C
    temp_ambiente = random.uniform(18, 32)
    temp_interna = temp_ambiente + random.uniform(3, 8)
    return round(temp_interna, 1)

def generar_payload_completo(tracker, velocidad):
    """Genera payload completo con todos los datos de un tracker real"""

    # Obtener celda según operador
    cell = CELL_TOWERS[0] if "Movistar" in tracker["networkOperator"] else CELL_TOWERS[1]

    # Consumo de batería simulado
    if tracker["solarPanel"] and 9 <= datetime.now().hour <= 18:  # Carga solar durante el día
        tracker["batteryLevel"] = min(100, tracker["batteryLevel"] + random.uniform(0, 0.5))
    else:
        tracker["batteryLevel"] = max(0, tracker["batteryLevel"] - random.uniform(0, 0.2))

    satelites = calcular_satelites_visibles()
    hdop = calcular_hdop()

    payload = {
        # Identificación del dispositivo
        "deviceId": tracker["deviceId"],
        "serialNumber": tracker["serialNumber"],
        "manufacturer": tracker["manufacturer"],
        "model": tracker["model"],
        "firmwareVersion": tracker["firmwareVersion"],

        # Datos GPS principales
        "latitude": round(tracker["lat"], 7),
        "longitude": round(tracker["lon"], 7),
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "altitude": round(random.uniform(15, 35), 2),  # Altitud típica de Gualeguaychú

        # Datos de movimiento
        "speed": round(velocidad, 2),
        "heading": round(random.uniform(0, 360), 2),
        "speedAccuracy": round(random.uniform(0.5, 2.0), 2),

        # Calidad de señal GPS
        "accuracy": round(random.uniform(3, 12), 2),
        "horizontalAccuracy": round(random.uniform(3, 10), 2),
        "verticalAccuracy": round(random.uniform(5, 15), 2),
        "hdop": hdop,
        "satellites": satelites,
        "gpsFixQuality": "3D" if satelites >= 4 else "2D",

        # Estado de la batería
        "batteryLevel": round(tracker["batteryLevel"], 1),
        "batteryVoltage": round(3.7 + (tracker["batteryLevel"] / 100) * 0.5, 2),  # 3.7-4.2V
        "chargingStatus": "solar" if tracker["solarPanel"] and 9 <= datetime.now().hour <= 18 else "battery",
        "batteryTemperature": round(random.uniform(20, 40), 1),

        # Red celular
        "signalStrength": calcular_signal_strength(),
        "networkType": "4G LTE",
        "operator": tracker["networkOperator"],
        "mcc": cell["mcc"],
        "mnc": cell["mnc"],
        "lac": cell["lac"],
        "cellId": cell["cellid"],

        # Sensores del dispositivo
        "internalTemperature": simular_temperatura_interna(),
        "accelerometerX": round(random.uniform(-0.5, 0.5), 3),
        "accelerometerY": round(random.uniform(-0.5, 0.5), 3),
        "accelerometerZ": round(random.uniform(9.5, 10.2), 3),  # Gravedad

        # Configuración del tracker
        "transmissionInterval": INTERVALO_ACTUALIZACION,
        "transmissionPower": tracker["transmissionPower"],
        "gpsModule": tracker["gpsModule"],
        "modemModule": tracker["modemModule"]
    }

    return payload

def enviar_datos_gps(tracker):
    """Envía datos GPS a la API"""
    try:
        velocidad = simular_movimiento_realista(tracker)
        payload = generar_payload_completo(tracker, velocidad)

        response = requests.post(API_URL, json=payload, timeout=5)

        timestamp = datetime.now().strftime('%H:%M:%S')

        if response.status_code == 200:
            print(f"✅ [{timestamp}] {tracker['deviceId']} | "
                  f"GPS: ({payload['latitude']:.6f}, {payload['longitude']:.6f}) | "
                  f"Vel: {payload['speed']:.1f} km/h | "
                  f"Sat: {payload['satellites']} | "
                  f"Señal: {payload['signalStrength']} dBm | "
                  f"Bat: {payload['batteryLevel']:.1f}%")
            return True
        else:
            print(f"❌ [{timestamp}] {tracker['deviceId']} | Error {response.status_code}: {response.text[:50]}")
            return False

    except requests.exceptions.RequestException as e:
        print(f"⚠️ [{datetime.now().strftime('%H:%M:%S')}] {tracker['deviceId']} | Error de conexión: {str(e)[:40]}")
        return False

def main():
    print("=" * 100)
    print("🐄 EMULADOR GPS TRACKER GANADERO - GUALEGUAYCHÚ, ENTRE RÍOS")
    print("=" * 100)
    print(f"📍 Ubicación: Gualeguaychú, Entre Ríos, Argentina")
    print(f"🌐 API: {API_URL}")
    print(f"⏱️  Intervalo: {INTERVALO_ACTUALIZACION} segundos")
    print(f"📡 Trackers activos: {len(TRACKERS)}")
    print("=" * 100)

    for tracker in TRACKERS:
        print(f"\n📟 {tracker['deviceId']}")
        print(f"   └─ Nombre: {tracker['nombre']}")
        print(f"   └─ Serial: {tracker['serialNumber']}")
        print(f"   └─ Modelo: {tracker['manufacturer']} {tracker['model']}")
        print(f"   └─ Ubicación inicial: ({tracker['lat']:.6f}, {tracker['lon']:.6f})")
        print(f"   └─ Red: {tracker['networkOperator']} | Módulo: {tracker['modemModule']}")
        print(f"   └─ GPS: {tracker['gpsModule']} | Batería: {tracker['batteryLevel']}%")
        print(f"   └─ Panel Solar: {'Sí' if tracker['solarPanel'] else 'No'}")

    print("\n" + "=" * 100)
    print("🚀 Iniciando transmisión de datos GPS...")
    print("Presiona Ctrl+C para detener el emulador\n")

    ciclo = 0

    try:
        while True:
            ciclo += 1
            print(f"\n📊 Ciclo #{ciclo} - {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
            print("-" * 100)

            for tracker in TRACKERS:
                enviar_datos_gps(tracker)
                time.sleep(0.5)

            print(f"\n⏳ Esperando {INTERVALO_ACTUALIZACION} segundos hasta el próximo envío...")
            time.sleep(INTERVALO_ACTUALIZACION)

    except KeyboardInterrupt:
        print("\n\n🛑 Emulador detenido por el usuario")
        print("=" * 100)
        print(f"✅ Total de ciclos completados: {ciclo}")
        print("=" * 100)
        print()

if __name__ == "__main__":
    main()
