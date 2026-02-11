# -*- coding: utf-8 -*-
"""
Script de prueba para verificar el emulador GPS
"""
import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# Cargar el polígono del emulador
exec(open('emulador_gps_gualeguaychu_avanzado.py', encoding='utf-8').read().split('def enviar_update')[0])

print("=" * 80)
print("📊 VERIFICACIÓN DEL EMULADOR GPS")
print("=" * 80)
print(f"\n🗺️  Área de Geofencing:")
print(f"   - Puntos del polígono: {len(GEOFENCE_POLYGON)}")
print(f"   - Límites de Latitud: {LAT_MIN:.6f} a {LAT_MAX:.6f}")
print(f"   - Límites de Longitud: {LON_MIN:.6f} a {LON_MAX:.6f}")

print(f"\n📡 Trackers Configurados: {len(TRACKERS)}")
print("\n🐄 Posiciones Iniciales de Trackers:")
print("-" * 80)

campos_dict = {}
for tracker in TRACKERS:
    if tracker["campo"] not in campos_dict:
        campos_dict[tracker["campo"]] = []
    campos_dict[tracker["campo"]].append(tracker)

for campo, trackers in sorted(campos_dict.items()):
    print(f"\n🏞️  {campo} ({len(trackers)} trackers):")
    for t in trackers:
        dentro = "✅ DENTRO" if is_point_in_polygon(t["lat"], t["lon"], GEOFENCE_POLYGON) else "❌ FUERA"
        print(f"   {t['deviceId']:18} | {t['nombre']:40} | ({t['lat']:.6f}, {t['lon']:.6f}) | {dentro}")

# Contar trackers dentro y fuera
dentro_count = sum(1 for t in TRACKERS if is_point_in_polygon(t["lat"], t["lon"], GEOFENCE_POLYGON))
fuera_count = len(TRACKERS) - dentro_count

print("\n" + "=" * 80)
print(f"📈 Resumen: {dentro_count}/{len(TRACKERS)} trackers dentro del área ({dentro_count/len(TRACKERS)*100:.1f}%)")
if fuera_count > 0:
    print(f"⚠️  {fuera_count} trackers están FUERA del área de geofencing")
else:
    print("✅ Todos los trackers están dentro del área de geofencing")
print("=" * 80)
