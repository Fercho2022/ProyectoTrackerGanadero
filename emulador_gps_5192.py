import requests
import time
import random
import json
from datetime import datetime
import sys

# Configuración de la API
API_URL = "http://localhost:5192/api/gps/update"
INTERVALO_ACTUALIZACION = 10  # segundos

# Configuración de trackers
TRACKERS = [
    {
        "deviceId": "COW_GPS_ER_01",
        "nombre": "Vaca OjoManchado",
        "lat_inicial": -34.6037,
        "lon_inicial": -58.3816,
        "lat_actual": -34.6037,
        "lon_actual": -58.3816,
        "velocidad_max": 5.0,  # km/h
        "area_movimiento": 0.002,  # ~200 metros
        "estado": "activo"
    },
    {
        "deviceId": "COW_GPS_ER_02",
        "nombre": "Vaca OjosCafe",
        "lat_inicial": -34.6047,
        "lon_inicial": -58.3826,
        "lat_actual": -34.6047,
        "lon_actual": -58.3826,
        "velocidad_max": 4.5,
        "area_movimiento": 0.002,
        "estado": "activo"
    },
    {
        "deviceId": "COW_GPS_ER_03",
        "nombre": "Vaca PataBlanc",
        "lat_inicial": -34.6057,
        "lon_inicial": -58.3836,
        "lat_actual": -34.6057,
        "lon_actual": -58.3836,
        "velocidad_max": 3.5,
        "area_movimiento": 0.0015,
        "estado": "activo"
    }
]

# Estadísticas
estadisticas = {
    "enviados": 0,
    "exitosos": 0,
    "errores": 0,
    "inicio": None
}

def generar_datos_gps(tracker):
    """Genera datos GPS realistas para un tracker"""

    # Simular diferentes patrones de movimiento según el estado
    if tracker["estado"] == "activo":
        # Movimiento normal dentro del área definida
        lat_offset = random.uniform(-tracker["area_movimiento"], tracker["area_movimiento"])
        lon_offset = random.uniform(-tracker["area_movimiento"], tracker["area_movimiento"])

        # Aplicar offset pero mantener cerca de la posición inicial
        tracker["lat_actual"] = tracker["lat_inicial"] + lat_offset
        tracker["lon_actual"] = tracker["lon_inicial"] + lon_offset

        velocidad = random.uniform(0, tracker["velocidad_max"])

    elif tracker["estado"] == "inmovil":
        # Sin movimiento, velocidad 0
        velocidad = 0

    elif tracker["estado"] == "rapido":
        # Movimiento rápido (posible alerta)
        lat_offset = random.uniform(-tracker["area_movimiento"] * 2, tracker["area_movimiento"] * 2)
        lon_offset = random.uniform(-tracker["area_movimiento"] * 2, tracker["area_movimiento"] * 2)

        tracker["lat_actual"] = tracker["lat_inicial"] + lat_offset
        tracker["lon_actual"] = tracker["lon_inicial"] + lon_offset

        velocidad = random.uniform(tracker["velocidad_max"], tracker["velocidad_max"] * 2)
    else:
        velocidad = random.uniform(0, tracker["velocidad_max"])

    # Generar payload completo
    payload = {
        "deviceId": tracker["deviceId"],
        "latitude": round(tracker["lat_actual"], 6),
        "longitude": round(tracker["lon_actual"], 6),
        "timestamp": datetime.utcnow().isoformat() + "Z",
        "speed": round(velocidad, 2),
        "heading": round(random.uniform(0, 360), 2),
        "altitude": round(random.uniform(50, 150), 2),
        "accuracy": round(random.uniform(5, 15), 2),
        "batteryLevel": random.randint(60, 100),
        "signalStrength": random.randint(-90, -50)
    }

    return payload

def enviar_actualizacion_gps(tracker):
    """Envía actualización GPS a la API"""
    try:
        payload = generar_datos_gps(tracker)
        estadisticas["enviados"] += 1

        response = requests.post(API_URL, json=payload, timeout=5)

        if response.status_code == 200:
            estadisticas["exitosos"] += 1
            print(f"✅ {tracker['deviceId']:18} | Lat: {payload['latitude']:10.6f} | Lon: {payload['longitude']:10.6f} | Vel: {payload['speed']:5.2f} km/h | {tracker['estado'].upper()}")
            return True
        else:
            estadisticas["errores"] += 1
            print(f"❌ {tracker['deviceId']:18} | Error {response.status_code}: {response.text[:50]}")
            return False

    except requests.exceptions.Timeout:
        estadisticas["errores"] += 1
        print(f"⏱️ {tracker['deviceId']:18} | Timeout al conectar con la API")
        return False
    except requests.exceptions.ConnectionError:
        estadisticas["errores"] += 1
        print(f"🔌 {tracker['deviceId']:18} | No se pudo conectar a la API")
        return False
    except Exception as e:
        estadisticas["errores"] += 1
        print(f"⚠️ {tracker['deviceId']:18} | Error: {str(e)[:50]}")
        return False

def mostrar_estadisticas():
    """Muestra estadísticas de ejecución"""
    tiempo_transcurrido = (datetime.now() - estadisticas["inicio"]).total_seconds()
    minutos = int(tiempo_transcurrido // 60)
    segundos = int(tiempo_transcurrido % 60)

    tasa_exito = (estadisticas["exitosos"] / estadisticas["enviados"] * 100) if estadisticas["enviados"] > 0 else 0

    print("\n" + "=" * 90)
    print(f"📊 ESTADÍSTICAS | Tiempo: {minutos}m {segundos}s | Enviados: {estadisticas['enviados']} | "
          f"Exitosos: {estadisticas['exitosos']} | Errores: {estadisticas['errores']} | "
          f"Tasa éxito: {tasa_exito:.1f}%")
    print("=" * 90)

def cambiar_estado_aleatorio():
    """Cambia aleatoriamente el estado de algún tracker para simular comportamiento variado"""
    estados = ["activo", "activo", "activo", "inmovil", "rapido"]  # Mayor probabilidad de estar activo

    if random.random() < 0.1:  # 10% de probabilidad cada ciclo
        tracker = random.choice(TRACKERS)
        nuevo_estado = random.choice(estados)
        if tracker["estado"] != nuevo_estado:
            tracker["estado"] = nuevo_estado
            print(f"🔄 {tracker['deviceId']} cambió a estado: {nuevo_estado.upper()}")

def mostrar_menu_interactivo():
    """Muestra opciones de configuración"""
    print("\n📋 COMANDOS DISPONIBLES:")
    print("  - Presiona 'q' + Enter para salir")
    print("  - El emulador continuará enviando datos automáticamente\n")

def main():
    print("=" * 90)
    print("🐄 EMULADOR GPS AVANZADO - SISTEMA TRACKER GANADERO")
    print("=" * 90)
    print(f"🌐 API URL: {API_URL}")
    print(f"⏱️ Intervalo de actualización: {INTERVALO_ACTUALIZACION} segundos")
    print(f"📡 Trackers configurados: {len(TRACKERS)}")
    print("=" * 90)

    for tracker in TRACKERS:
        print(f"  📍 {tracker['deviceId']:18} | {tracker['nombre']:20} | "
              f"Pos: ({tracker['lat_inicial']:.6f}, {tracker['lon_inicial']:.6f}) | "
              f"Área: {tracker['area_movimiento']*111:.0f}m")

    print("=" * 90)
    mostrar_menu_interactivo()

    estadisticas["inicio"] = datetime.now()
    contador_ciclos = 0

    try:
        while True:
            timestamp = datetime.now().strftime('%H:%M:%S')
            print(f"\n⏰ Ciclo {contador_ciclos + 1} - {timestamp}")
            print("-" * 90)

            for tracker in TRACKERS:
                enviar_actualizacion_gps(tracker)
                time.sleep(0.5)  # Pequeña pausa entre trackers

            contador_ciclos += 1

            # Mostrar estadísticas cada 5 ciclos
            if contador_ciclos % 5 == 0:
                mostrar_estadisticas()

            # Cambiar estado aleatorio ocasionalmente
            cambiar_estado_aleatorio()

            print(f"\n⏳ Esperando {INTERVALO_ACTUALIZACION} segundos hasta el próximo ciclo...")
            time.sleep(INTERVALO_ACTUALIZACION)

    except KeyboardInterrupt:
        print("\n\n🛑 Emulador detenido por el usuario")
        mostrar_estadisticas()
        print("\n✅ Emulador finalizado correctamente\n")
    except Exception as e:
        print(f"\n\n❌ Error inesperado: {str(e)}")
        mostrar_estadisticas()
        sys.exit(1)

if __name__ == "__main__":
    main()
