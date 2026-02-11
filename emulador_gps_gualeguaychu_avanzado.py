"""
Emulador GPS Avanzado para Trackers Ganaderos
Gualeguaychú, Entre Ríos, Argentina
Sistema completo con monitoreo, estadísticas y simulación de eventos
MODIFICADO: Movimiento dentro de área de geofencing personalizada
"""

import requests
import time
import random
from datetime import datetime, timezone
import json
import sys
from collections import defaultdict

# Configurar codificación UTF-8 para emojis en Windows
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# Configuración API
API_URL = "http://localhost:5192/api/gps/update"
INTERVALO_ACTUALIZACION = 10  # segundos

# Definición del polígono de geofencing (34 puntos - Nueva área)
GEOFENCE_POLYGON = [
    (-33.011773, -60.499933), (-33.015062, -60.496380), (-33.021683, -60.492172), (-33.026569, -60.488480),
    (-33.031463, -60.484788), (-33.036773, -60.482370), (-33.042097, -60.482542), (-33.048717, -60.485976),
    (-33.056918, -60.486234), (-33.061881, -60.483658), (-33.068139, -60.477647), (-33.074541, -60.470692),
    (-33.079216, -60.464510), (-33.081373, -60.459787), (-33.082955, -60.452488), (-33.081948, -60.444846),
    (-33.079935, -60.440295), (-33.076698, -60.434628), (-33.070801, -60.432396), (-33.064831, -60.432138),
    (-33.061932, -60.432034), (-33.059306, -60.429887), (-33.057605, -60.428062), (-33.030772, -60.435587),
    (-33.013932, -60.447093), (-32.997233, -60.459801), (-32.985138, -60.471307), (-32.979523, -60.486248),
    (-32.978371, -60.500674), (-32.979523, -60.508573), (-32.981827, -60.515443), (-32.991618, -60.515271),
    (-33.000429, -60.511664), (-33.006187, -60.506512)
]

# Calcular bounds del polígono
LAT_MIN = min(p[0] for p in GEOFENCE_POLYGON)
LAT_MAX = max(p[0] for p in GEOFENCE_POLYGON)
LON_MIN = min(p[1] for p in GEOFENCE_POLYGON)
LON_MAX = max(p[1] for p in GEOFENCE_POLYGON)

def is_point_in_polygon(lat, lon, polygon):
    """
    Determina si un punto está dentro de un polígono usando el algoritmo de ray casting
    """
    x, y = lon, lat
    n = len(polygon)
    inside = False

    p1_lat, p1_lon = polygon[0]
    for i in range(1, n + 1):
        p2_lat, p2_lon = polygon[i % n]
        if y > min(p1_lat, p2_lat):
            if y <= max(p1_lat, p2_lat):
                if x <= max(p1_lon, p2_lon):
                    if p1_lat != p2_lat:
                        xinters = (y - p1_lat) * (p2_lon - p1_lon) / (p2_lat - p1_lat) + p1_lon
                    if p1_lon == p2_lon or x <= xinters:
                        inside = not inside
        p1_lat, p1_lon = p2_lat, p2_lon

    return inside

def generar_punto_aleatorio_dentro_poligono(max_intentos=100):
    """
    Genera un punto aleatorio dentro del polígono de geofencing
    """
    for _ in range(max_intentos):
        lat = random.uniform(LAT_MIN, LAT_MAX)
        lon = random.uniform(LON_MIN, LON_MAX)
        if is_point_in_polygon(lat, lon, GEOFENCE_POLYGON):
            return lat, lon

    # Si no se encuentra punto después de max_intentos, usar un punto conocido del polígono
    return GEOFENCE_POLYGON[len(GEOFENCE_POLYGON) // 2]

# Configuración completa de 15 Trackers GPS - POSICIONES INICIALES DENTRO DEL POLÍGONO
print("🔧 Generando posiciones iniciales aleatorias dentro del área de geofencing...")
TRACKERS = []

trackers_config = [
    # Campo La Esperanza - 5 trackers
    {"deviceId": "COW_GPS_ER_01", "nombre": "Vaca Manchada - Campo La Esperanza", "campo": "La Esperanza"},
    {"deviceId": "COW_GPS_ER_02", "nombre": "Vaca Castaña - Campo La Esperanza", "campo": "La Esperanza"},
    {"deviceId": "COW_GPS_ER_03", "nombre": "Vaca Negra - Campo La Esperanza", "campo": "La Esperanza"},
    {"deviceId": "COW_GPS_ER_04", "nombre": "Vaca Blanca - Campo La Esperanza", "campo": "La Esperanza"},
    {"deviceId": "COW_GPS_ER_05", "nombre": "Vaca Colorada - Campo La Esperanza", "campo": "La Esperanza"},

    # Campo San Jorge - 5 trackers
    {"deviceId": "COW_GPS_ER_06", "nombre": "Toro Hornero - Campo San Jorge", "campo": "San Jorge"},
    {"deviceId": "COW_GPS_ER_07", "nombre": "Vaca Moteada - Campo San Jorge", "campo": "San Jorge"},
    {"deviceId": "COW_GPS_ER_08", "nombre": "Vaca Pampa - Campo San Jorge", "campo": "San Jorge"},
    {"deviceId": "COW_GPS_ER_09", "nombre": "Vaca Overa - Campo San Jorge", "campo": "San Jorge"},
    {"deviceId": "COW_GPS_ER_10", "nombre": "Vaca Rubia - Campo San Jorge", "campo": "San Jorge"},

    # Campo El Ombú - 5 trackers
    {"deviceId": "COW_GPS_ER_11", "nombre": "Vaca Bonita - Campo El Ombú", "campo": "El Ombú"},
    {"deviceId": "COW_GPS_ER_12", "nombre": "Vaca Prieta - Campo El Ombú", "campo": "El Ombú"},
    {"deviceId": "COW_GPS_ER_13", "nombre": "Vaca Zaino - Campo El Ombú", "campo": "El Ombú"},
    {"deviceId": "COW_GPS_ER_14", "nombre": "Vaca Gateada - Campo El Ombú", "campo": "El Ombú"},
    {"deviceId": "COW_GPS_ER_15", "nombre": "Vaca Rosada - Campo El Ombú", "campo": "El Ombú"},
]

# Generar trackers con posiciones aleatorias dentro del polígono
for i, config in enumerate(trackers_config):
    lat, lon = generar_punto_aleatorio_dentro_poligono()

    # Alternar entre modelos y configuraciones
    is_pro = i % 3 != 2
    solar = i % 2 == 0

    tracker = {
        "deviceId": config["deviceId"],
        "serialNumber": f"GPS-ER-2024-{(i+1):03d}",
        "manufacturer": "TechTrack Argentina",
        "model": "TT-4G-PRO" if is_pro else "TT-4G-LITE",
        "firmwareVersion": "2.4.1" if is_pro else "2.3.8",
        "hardwareVersion": "HW-3.2" if is_pro else "HW-3.1",
        "lat": lat,
        "lon": lon,
        "nombre": config["nombre"],
        "batteryCapacity": 5000 if is_pro else 4000,
        "batteryLevel": random.randint(80, 95),
        "solarPanel": solar,
        "transmissionPower": -10 if is_pro else -12,
        "gpsModule": "Quectel L86" if is_pro else "Quectel L76",
        "modemModule": "Quectel EC25" if is_pro else "Quectel EC21",
        "networkOperator": "Movistar AR" if i % 2 == 0 else "Claro AR",
        "velocidad_max": random.uniform(3.5, 4.5),
        "area_movimiento": 0.003,
        "estado": random.choice(["pastando", "pastando", "pastando", "descansando"]),
        "campo": config["campo"]
    }
    TRACKERS.append(tracker)

# Torres celulares
CELL_TOWERS = [
    {"mcc": 722, "mnc": 70, "lac": 1234, "cellid": 56781, "operator": "Movistar AR"},
    {"mcc": 722, "mnc": 310, "lac": 1235, "cellid": 56782, "operator": "Claro AR"},
]

# Estadísticas globales
stats = {
    "ciclos": 0,
    "enviados": 0,
    "exitosos": 0,
    "errores": 0,
    "inicio": None,
    "rechazos_geofence": 0,  # Nueva estadística
    "por_tracker": defaultdict(lambda: {"enviados": 0, "exitosos": 0, "errores": 0, "rechazos": 0}),
    "por_campo": defaultdict(lambda: {"enviados": 0, "exitosos": 0})
}

# Estados posibles y sus características
ESTADOS = {
    "pastando": {"velocidad_mult": 1.0, "movimiento_mult": 1.0, "prob": 0.6},
    "descansando": {"velocidad_mult": 0.1, "movimiento_mult": 0.2, "prob": 0.25},
    "caminando": {"velocidad_mult": 1.5, "movimiento_mult": 1.3, "prob": 0.1},
    "alerta": {"velocidad_mult": 2.0, "movimiento_mult": 1.8, "prob": 0.05}
}

def cambiar_estado_aleatorio():
    """Cambia el estado de los trackers aleatoriamente"""
    for tracker in TRACKERS:
        if random.random() < 0.08:  # 8% probabilidad por ciclo
            nuevo_estado = random.choices(
                list(ESTADOS.keys()),
                weights=[ESTADOS[e]["prob"] for e in ESTADOS.keys()]
            )[0]
            if tracker["estado"] != nuevo_estado:
                tracker["estado"] = nuevo_estado
                emoji = {"pastando": "🌾", "descansando": "😴", "caminando": "🚶", "alerta": "⚠️"}
                print(f"   {emoji[nuevo_estado]} {tracker['deviceId']} → Estado: {nuevo_estado.upper()}")

def simular_movimiento(tracker):
    """
    Simula movimiento realista según el estado del animal
    MODIFICADO: Valida que el movimiento permanezca dentro del geofencing
    INCLUYE: Probabilidad de escape para generar alertas de geofencing
    """
    estado_config = ESTADOS[tracker["estado"]]

    # Guardar posición actual por si necesitamos revertir
    lat_actual = tracker["lat"]
    lon_actual = tracker["lon"]
    esta_dentro = is_point_in_polygon(lat_actual, lon_actual, GEOFENCE_POLYGON)

    # PROBABILIDAD DE ESCAPE: 8% de probabilidad de intentar salir del área
    # Mayor probabilidad si está en estado "alerta" (15%)
    prob_escape = 0.15 if tracker["estado"] == "alerta" else 0.08
    intentar_escape = random.random() < prob_escape

    # Si está fuera, intentar volver al área con 50% de probabilidad
    if not esta_dentro and random.random() < 0.5:
        # Calcular dirección hacia el centro del polígono
        centro_lat = (LAT_MIN + LAT_MAX) / 2
        centro_lon = (LON_MIN + LON_MAX) / 2

        # Mover hacia el centro
        lat_offset = (centro_lat - lat_actual) * 0.05  # Avanzar 5% hacia el centro
        lon_offset = (centro_lon - lon_actual) * 0.05

        tracker["lat"] = lat_actual + lat_offset
        tracker["lon"] = lon_actual + lon_offset

        velocidad = random.uniform(2.0, 4.0)  # Velocidad moderada de regreso
        return velocidad

    # Si intentamos escape, hacer movimiento más grande y permitir salir
    if intentar_escape and esta_dentro:
        velocidad = random.uniform(3.0, 6.0)  # Velocidad alta para escape
        area = tracker["area_movimiento"] * 2.5  # Área más grande
        lat_offset = random.uniform(-area, area)
        lon_offset = random.uniform(-area, area)

        tracker["lat"] = lat_actual + lat_offset
        tracker["lon"] = lon_actual + lon_offset

        # Marcar que salió del área (para tracking)
        if not is_point_in_polygon(tracker["lat"], tracker["lon"], GEOFENCE_POLYGON):
            tracker["estado"] = "alerta"  # Cambiar a estado alerta

        return velocidad

    # Movimiento normal: intentar hasta 10 veces encontrar un movimiento válido dentro del polígono
    for intento in range(10):
        if tracker["estado"] == "descansando":
            velocidad = random.uniform(0, 0.5)
            lat_offset = random.uniform(-0.0001, 0.0001)
            lon_offset = random.uniform(-0.0001, 0.0001)
        else:
            velocidad = random.uniform(0, tracker["velocidad_max"] * estado_config["velocidad_mult"])
            area = tracker["area_movimiento"] * estado_config["movimiento_mult"]
            lat_offset = random.uniform(-area, area)
            lon_offset = random.uniform(-area, area)

        # Calcular nueva posición
        nueva_lat = lat_actual + lat_offset
        nueva_lon = lon_actual + lon_offset

        # Verificar si la nueva posición está dentro del polígono
        if is_point_in_polygon(nueva_lat, nueva_lon, GEOFENCE_POLYGON):
            tracker["lat"] = nueva_lat
            tracker["lon"] = nueva_lon
            return velocidad

        # Si no está dentro, reducir el área de movimiento para el siguiente intento
        area = area * 0.5

    # Si después de 10 intentos no se encuentra posición válida, mantener posición actual
    stats["rechazos_geofence"] += 1
    stats["por_tracker"][tracker["deviceId"]]["rechazos"] += 1
    return 0.0  # Velocidad 0 indica que no se movió

def generar_payload_completo(tracker, velocidad):
    """Genera payload completo con datos realistas"""
    cell = next((t for t in CELL_TOWERS if t["operator"] == tracker["networkOperator"]), CELL_TOWERS[0])

    # Gestión de batería
    hora_actual = datetime.now().hour
    if tracker["solarPanel"] and 9 <= hora_actual <= 18:
        tracker["batteryLevel"] = min(100, tracker["batteryLevel"] + random.uniform(0.1, 0.5))
    else:
        consumo = 0.15 if tracker["estado"] == "alerta" else 0.08
        tracker["batteryLevel"] = max(0, tracker["batteryLevel"] - random.uniform(0, consumo))

    satelites = random.randint(7, 12)
    hdop = round(random.uniform(0.8, 2.5), 1)

    payload = {
        # Identificación
        "deviceId": tracker["deviceId"],
        "serialNumber": tracker["serialNumber"],
        "manufacturer": tracker["manufacturer"],
        "model": tracker["model"],
        "firmwareVersion": tracker["firmwareVersion"],

        # GPS
        "latitude": round(tracker["lat"], 7),
        "longitude": round(tracker["lon"], 7),
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "altitude": round(random.uniform(15, 35), 2),
        "speed": round(velocidad, 2),
        "heading": round(random.uniform(0, 360), 2),
        "speedAccuracy": round(random.uniform(0.5, 2.0), 2),

        # Calidad GPS
        "accuracy": round(random.uniform(3, 12), 2),
        "horizontalAccuracy": round(random.uniform(3, 10), 2),
        "verticalAccuracy": round(random.uniform(5, 15), 2),
        "hdop": hdop,
        "satellites": satelites,
        "gpsFixQuality": "3D" if satelites >= 4 else "2D",

        # Batería
        "batteryLevel": round(tracker["batteryLevel"], 1),
        "batteryVoltage": round(3.7 + (tracker["batteryLevel"] / 100) * 0.5, 2),
        "chargingStatus": "solar" if tracker["solarPanel"] and 9 <= hora_actual <= 18 else "battery",
        "batteryTemperature": round(random.uniform(20, 40), 1),

        # Red celular
        "signalStrength": random.randint(-85, -55),
        "networkType": "4G LTE",
        "operator": tracker["networkOperator"],
        "mcc": cell["mcc"],
        "mnc": cell["mnc"],
        "lac": cell["lac"],
        "cellId": cell["cellid"],

        # Sensores
        "internalTemperature": round(random.uniform(25, 45), 1),
        "accelerometerX": round(random.uniform(-0.5, 0.5), 3),
        "accelerometerY": round(random.uniform(-0.5, 0.5), 3),
        "accelerometerZ": round(random.uniform(9.5, 10.2), 3),

        # Configuración
        "transmissionInterval": INTERVALO_ACTUALIZACION,
        "transmissionPower": tracker["transmissionPower"],
        "gpsModule": tracker["gpsModule"],
        "modemModule": tracker["modemModule"]
    }

    return payload

def enviar_datos(tracker):
    """Envía datos GPS a la API"""
    try:
        velocidad = simular_movimiento(tracker)
        payload = generar_payload_completo(tracker, velocidad)

        stats["enviados"] += 1
        stats["por_tracker"][tracker["deviceId"]]["enviados"] += 1
        stats["por_campo"][tracker["campo"]]["enviados"] += 1

        response = requests.post(API_URL, json=payload, timeout=5)

        if response.status_code == 200:
            stats["exitosos"] += 1
            stats["por_tracker"][tracker["deviceId"]]["exitosos"] += 1
            stats["por_campo"][tracker["campo"]]["exitosos"] += 1

            estado_emoji = {"pastando": "🌾", "descansando": "😴", "caminando": "🚶", "alerta": "⚠️"}
            esta_dentro = is_point_in_polygon(tracker["lat"], tracker["lon"], GEOFENCE_POLYGON)
            dentro_emoji = "✅ DENTRO " if esta_dentro else "🚨 FUERA  "
            print(f"  {dentro_emoji} {tracker['deviceId']:18} | {tracker['campo']:15} | "
                  f"GPS: ({payload['latitude']:9.6f}, {payload['longitude']:9.6f}) | "
                  f"Vel: {payload['speed']:4.1f} | Sat: {payload['satellites']:2} | "
                  f"Bat: {payload['batteryLevel']:5.1f}% | {estado_emoji[tracker['estado']]} {tracker['estado']}")
            return True
        else:
            stats["errores"] += 1
            stats["por_tracker"][tracker["deviceId"]]["errores"] += 1
            error_msg = response.text[:100] if response.text else "No message"
            print(f"  ❌ {tracker['deviceId']:18} | Error {response.status_code}: {error_msg}")
            return False

    except Exception as e:
        stats["errores"] += 1
        stats["por_tracker"][tracker["deviceId"]]["errores"] += 1
        print(f"  ⚠️ {tracker['deviceId']:18} | Error: {str(e)[:30]}")
        return False

def mostrar_estadisticas_detalladas():
    """Muestra estadísticas completas"""
    tiempo = (datetime.now() - stats["inicio"]).total_seconds()
    mins, segs = int(tiempo // 60), int(tiempo % 60)
    tasa_exito = (stats["exitosos"] / stats["enviados"] * 100) if stats["enviados"] > 0 else 0

    # Contar trackers fuera del área
    trackers_fuera = sum(1 for t in TRACKERS if not is_point_in_polygon(t["lat"], t["lon"], GEOFENCE_POLYGON))

    print("\n" + "=" * 120)
    print(f"📊 ESTADÍSTICAS GLOBALES | ⏱️ Tiempo: {mins}m {segs}s | 🔄 Ciclos: {stats['ciclos']} | "
          f"📤 Enviados: {stats['enviados']} | ✅ Exitosos: {stats['exitosos']} | "
          f"❌ Errores: {stats['errores']} | 🚧 Rechazos geofence: {stats['rechazos_geofence']} | "
          f"🚨 Fuera del área: {trackers_fuera}/{len(TRACKERS)} | "
          f"📈 Tasa éxito: {tasa_exito:.1f}%")
    print("=" * 120)

    # Estadísticas por campo
    print("\n📍 ESTADÍSTICAS POR CAMPO:")
    for campo, data in sorted(stats["por_campo"].items()):
        tasa = (data["exitosos"] / data["enviados"] * 100) if data["enviados"] > 0 else 0
        print(f"   🏞️ {campo:20} | Enviados: {data['enviados']:4} | Exitosos: {data['exitosos']:4} | Tasa: {tasa:5.1f}%")

    # Mostrar trackers que están fuera
    if trackers_fuera > 0:
        print(f"\n🚨 TRACKERS FUERA DEL ÁREA DE GEOFENCING ({trackers_fuera}):")
        for t in TRACKERS:
            if not is_point_in_polygon(t["lat"], t["lon"], GEOFENCE_POLYGON):
                print(f"   ⚠️  {t['deviceId']:18} | {t['nombre']:35} | ({t['lat']:.6f}, {t['lon']:.6f})")

def main():
    print("=" * 120)
    print("🐄 EMULADOR GPS AVANZADO - SISTEMA TRACKER GANADERO")
    print("🔒 MODO GEOFENCING ACTIVO - MOVIMIENTO RESTRINGIDO AL ÁREA DEFINIDA")
    print("=" * 120)
    print(f"🌐 API: {API_URL}")
    print(f"⏱️  Intervalo: {INTERVALO_ACTUALIZACION} segundos")
    print(f"📡 Trackers: {len(TRACKERS)} dispositivos en 3 campos")
    print(f"🗺️  Área de geofencing: {len(GEOFENCE_POLYGON)} puntos")
    print(f"📏 Límites: Lat ({LAT_MIN:.6f}, {LAT_MAX:.6f}) | Lon ({LON_MIN:.6f}, {LON_MAX:.6f})")
    print("=" * 120)

    # Mostrar trackers agrupados por campo
    campos = {}
    for tracker in TRACKERS:
        if tracker["campo"] not in campos:
            campos[tracker["campo"]] = []
        campos[tracker["campo"]].append(tracker)

    for campo, trackers in sorted(campos.items()):
        print(f"\n🏞️ CAMPO {campo.upper()} ({len(trackers)} trackers):")
        for t in trackers:
            solar_icon = "☀️" if t["solarPanel"] else "🔋"
            dentro = "✅ DENTRO" if is_point_in_polygon(t["lat"], t["lon"], GEOFENCE_POLYGON) else "❌ FUERA"
            print(f"   📟 {t['deviceId']} | {t['nombre']:35} | ({t['lat']:.6f}, {t['lon']:.6f}) | "
                  f"{dentro} | {t['model']:12} | {solar_icon} {t['batteryLevel']:.0f}% | {t['networkOperator']}")

    print("\n" + "=" * 120)
    print("🚀 Iniciando transmisión de datos GPS...\n")

    stats["inicio"] = datetime.now()

    try:
        while True:
            stats["ciclos"] += 1
            print(f"\n{'='*120}")
            print(f"⏰ CICLO #{stats['ciclos']} - {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
            print(f"{'='*120}")

            for tracker in TRACKERS:
                enviar_datos(tracker)
                time.sleep(0.3)

            # Cambios de estado aleatorios
            if stats["ciclos"] % 3 == 0:
                print("\n🔄 Cambios de estado:")
                cambiar_estado_aleatorio()

            # Estadísticas cada 5 ciclos
            if stats["ciclos"] % 5 == 0:
                mostrar_estadisticas_detalladas()

            print(f"\n⏳ Esperando {INTERVALO_ACTUALIZACION} segundos...")
            time.sleep(INTERVALO_ACTUALIZACION)

    except KeyboardInterrupt:
        print("\n\n🛑 Emulador detenido por el usuario")
        mostrar_estadisticas_detalladas()
        print("\n✅ Emulador finalizado\n")
    except Exception as e:
        print(f"\n\n❌ Error: {e}")
        sys.exit(1)

if __name__ == "__main__":
    main()
