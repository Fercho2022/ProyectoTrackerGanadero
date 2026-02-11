import requests
import time
import random
from datetime import datetime

# Configuración de la API
API_URL = "http://localhost:5192/api/gps/update"

# Lista de trackers para emular
TRACKERS = [
    {
        "deviceId": "COW_GPS_ER_01",
        "lat": -34.6037,
        "lon": -58.3816,
        "name": "Tracker 1"
    },
    {
        "deviceId": "COW_GPS_ER_02",
        "lat": -34.6047,
        "lon": -58.3826,
        "name": "Tracker 2"
    }
]

def enviar_posicion_gps(device_id, lat, lon):
    """Envía una actualización de posición GPS a la API"""
    try:
        payload = {
            "deviceId": device_id,
            "latitude": lat,
            "longitude": lon,
            "timestamp": datetime.utcnow().isoformat() + "Z",
            "speed": round(random.uniform(0, 5), 2),
            "heading": round(random.uniform(0, 360), 2),
            "altitude": round(random.uniform(50, 100), 2),
            "accuracy": round(random.uniform(5, 15), 2)
        }

        response = requests.post(API_URL, json=payload)

        if response.status_code == 200:
            print(f"✅ [{datetime.now().strftime('%H:%M:%S')}] {device_id}: Posición enviada ({lat:.6f}, {lon:.6f})")
        else:
            print(f"❌ [{datetime.now().strftime('%H:%M:%S')}] {device_id}: Error {response.status_code} - {response.text}")

    except Exception as e:
        print(f"⚠️ [{datetime.now().strftime('%H:%M:%S')}] {device_id}: Error al enviar - {str(e)}")

def simular_movimiento(tracker):
    """Simula movimiento aleatorio del tracker"""
    # Movimiento aleatorio pequeño (dentro de un radio de ~100 metros)
    lat_offset = random.uniform(-0.0009, 0.0009)
    lon_offset = random.uniform(-0.0009, 0.0009)

    nueva_lat = tracker["lat"] + lat_offset
    nueva_lon = tracker["lon"] + lon_offset

    # Actualizar posición del tracker
    tracker["lat"] = nueva_lat
    tracker["lon"] = nueva_lon

    return nueva_lat, nueva_lon

def main():
    print("=" * 60)
    print("🐄 EMULADOR GPS SIMPLE - TRACKER GANADERO")
    print("=" * 60)
    print(f"API URL: {API_URL}")
    print(f"Trackers activos: {len(TRACKERS)}")
    for tracker in TRACKERS:
        print(f"  - {tracker['deviceId']} ({tracker['name']})")
    print("=" * 60)
    print("Enviando actualizaciones GPS cada 10 segundos...")
    print("Presiona Ctrl+C para detener\n")

    try:
        while True:
            for tracker in TRACKERS:
                lat, lon = simular_movimiento(tracker)
                enviar_posicion_gps(tracker["deviceId"], lat, lon)

            time.sleep(10)

    except KeyboardInterrupt:
        print("\n\n⏹️ Emulador detenido por el usuario")
    except Exception as e:
        print(f"\n\n❌ Error inesperado: {str(e)}")

if __name__ == "__main__":
    main()
