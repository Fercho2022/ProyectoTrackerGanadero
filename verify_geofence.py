# -*- coding: utf-8 -*-
"""
Verificación rápida del área de geofencing
"""
import sys
import io
import random
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# Nuevo polígono de geofencing (34 puntos)
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

LAT_MIN = min(p[0] for p in GEOFENCE_POLYGON)
LAT_MAX = max(p[0] for p in GEOFENCE_POLYGON)
LON_MIN = min(p[1] for p in GEOFENCE_POLYGON)
LON_MAX = max(p[1] for p in GEOFENCE_POLYGON)

def is_point_in_polygon(lat, lon, polygon):
    """Determina si un punto está dentro de un polígono usando ray casting"""
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

def generar_punto_aleatorio(max_intentos=100):
    """Genera un punto aleatorio dentro del polígono"""
    for _ in range(max_intentos):
        lat = random.uniform(LAT_MIN, LAT_MAX)
        lon = random.uniform(LON_MIN, LON_MAX)
        if is_point_in_polygon(lat, lon, GEOFENCE_POLYGON):
            return lat, lon
    return GEOFENCE_POLYGON[len(GEOFENCE_POLYGON) // 2]

print("=" * 80)
print("🗺️  NUEVA ÁREA DE GEOFENCING - VERIFICACIÓN")
print("=" * 80)
print(f"\n📍 Puntos del polígono: {len(GEOFENCE_POLYGON)}")
print(f"📏 Límites:")
print(f"   Latitud:  {LAT_MIN:.6f} a {LAT_MAX:.6f}")
print(f"   Longitud: {LON_MIN:.6f} a {LON_MAX:.6f}")

print(f"\n✅ Generando 15 posiciones aleatorias de prueba...")
print("-" * 80)

trackers_test = [
    "Rebo004", "Rebo005", "Rebo001", "Rebo002", "Rebo003",
    "COW_GPS_ER_06", "COW_GPS_ER_07", "COW_GPS_ER_08", "COW_GPS_ER_09", "COW_GPS_ER_10",
    "COW_GPS_ER_11", "COW_GPS_ER_12", "COW_GPS_ER_13", "COW_GPS_ER_14", "COW_GPS_ER_15"
]

dentro_count = 0
for i, tracker_id in enumerate(trackers_test, 1):
    lat, lon = generar_punto_aleatorio()
    dentro = is_point_in_polygon(lat, lon, GEOFENCE_POLYGON)
    estado = "✅ DENTRO" if dentro else "❌ FUERA"
    if dentro:
        dentro_count += 1
    print(f"{i:2}. {tracker_id:18} | Lat: {lat:.6f}, Lng: {lon:.6f} | {estado}")

print("\n" + "=" * 80)
print(f"📊 Resultado: {dentro_count}/{len(trackers_test)} posiciones generadas dentro del área ({dentro_count/len(trackers_test)*100:.0f}%)")
print("=" * 80)

# Verificar algunos puntos conocidos del polígono
print(f"\n🔍 Verificando puntos del polígono original:")
for i in [0, len(GEOFENCE_POLYGON)//4, len(GEOFENCE_POLYGON)//2, -1]:
    lat, lon = GEOFENCE_POLYGON[i]
    dentro = is_point_in_polygon(lat, lon, GEOFENCE_POLYGON)
    estado = "✅ DENTRO" if dentro else "❌ FUERA"
    print(f"   Punto {i:2}: ({lat:.6f}, {lon:.6f}) | {estado}")
