"""
Emulador GPS - Test de Fuera de Limites (Geofencing)
Gualeguaychu, Entre Rios, Argentina

2 trackers salen y entran aleatoriamente del area de geofencing.
El resto de los trackers se mueven normalmente dentro del poligono.
Sirve para verificar que los marcadores cambian de borde blanco (dentro) a rojo (fuera).
"""

import requests
import time
import random
from requests.adapters import HTTPAdapter
import math
import threading
from datetime import datetime, timezone
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor
import sys
import io

# Codificacion UTF-8 para emojis en Windows
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

# ============================================================================
# CONFIGURACION
# ============================================================================

API_URL = "http://localhost:5192/api/gps/update"
CAMPO_NOMBRE = "La Esperanza"
TORO_DEVICE_ID = "BULL_GPS_ER_001"

NUM_TRACKERS = 15
NUM_ESCAPISTAS = 2       # Cantidad de trackers que salen y entran del geofence
INTERVALO_TRANSMISION = 10  # Segundos entre transmisiones
STATS_CADA_CICLOS = 5

# ============================================================================
# POLIGONO DE GEOFENCING (18 puntos - Gualeguaychu)
# ============================================================================

GEOFENCE_POLYGON = [
    (-33.059810, -60.485645), (-33.044702, -60.483584), (-33.028642, -60.486746), (-33.008779, -60.503404),
    (-32.997118, -60.485372), (-33.001149, -60.476099), (-33.016696, -60.467684), (-33.022021, -60.460986),
    (-33.028930, -60.453087), (-33.042745, -60.443641), (-33.051235, -60.439863), (-33.059148, -60.430761),
    (-33.064039, -60.432135), (-33.074253, -60.433337), (-33.081733, -60.445702), (-33.082883, -60.455319),
    (-33.079863, -60.464249), (-33.068643, -60.477301),
]

LAT_MIN = min(p[0] for p in GEOFENCE_POLYGON)
LAT_MAX = max(p[0] for p in GEOFENCE_POLYGON)
LON_MIN = min(p[1] for p in GEOFENCE_POLYGON)
LON_MAX = max(p[1] for p in GEOFENCE_POLYGON)

# Torres celulares
CELL_TOWERS = [
    {"mcc": 722, "mnc": 70, "lac": 1234, "cellid": 56781, "operator": "Movistar AR"},
    {"mcc": 722, "mnc": 310, "lac": 1235, "cellid": 56782, "operator": "Claro AR"},
]

# ============================================================================
# CONFIGURACION DE RED
# ============================================================================
session = requests.Session()
adapter = HTTPAdapter(pool_connections=50, pool_maxsize=50)
session.mount('http://', adapter)
session.mount('https://', adapter)

limiter_lock = threading.Lock()
last_api_call_time = 0.0


# ============================================================================
# FUNCIONES GEOMETRICAS
# ============================================================================

def is_point_in_polygon(lat, lon, polygon):
    """Determina si un punto esta dentro de un poligono (ray casting)"""
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


def generar_punto_dentro(max_intentos=100):
    """Genera un punto aleatorio dentro del poligono"""
    for _ in range(max_intentos):
        lat = random.uniform(LAT_MIN, LAT_MAX)
        lon = random.uniform(LON_MIN, LON_MAX)
        if is_point_in_polygon(lat, lon, GEOFENCE_POLYGON):
            return lat, lon
    return GEOFENCE_POLYGON[len(GEOFENCE_POLYGON) // 2]


def generar_punto_fuera():
    """Genera un punto justo fuera del poligono (cerca del borde)"""
    # Elegir un punto del borde del poligono y moverlo hacia afuera
    idx = random.randint(0, len(GEOFENCE_POLYGON) - 1)
    borde_lat, borde_lon = GEOFENCE_POLYGON[idx]

    # Calcular centroide
    centro_lat = sum(p[0] for p in GEOFENCE_POLYGON) / len(GEOFENCE_POLYGON)
    centro_lon = sum(p[1] for p in GEOFENCE_POLYGON) / len(GEOFENCE_POLYGON)

    # Direccion desde el centro hacia el borde (y un poco mas alla)
    dir_lat = borde_lat - centro_lat
    dir_lon = borde_lon - centro_lon
    dist = math.sqrt(dir_lat**2 + dir_lon**2)
    if dist > 0:
        dir_lat /= dist
        dir_lon /= dist

    # Mover entre 500m y 1.5km fuera del borde
    offset = random.uniform(0.005, 0.015)
    fuera_lat = borde_lat + dir_lat * offset
    fuera_lon = borde_lon + dir_lon * offset

    # Verificar que esta fuera
    if not is_point_in_polygon(fuera_lat, fuera_lon, GEOFENCE_POLYGON):
        return fuera_lat, fuera_lon

    # Si por alguna razon quedo dentro, mover mas lejos
    fuera_lat = borde_lat + dir_lat * 0.02
    fuera_lon = borde_lon + dir_lon * 0.02
    return fuera_lat, fuera_lon


def encontrar_punto_borde_cercano(lat, lon):
    """Encuentra el punto del borde del poligono mas cercano"""
    min_dist = float('inf')
    mejor_idx = 0
    for i, (p_lat, p_lon) in enumerate(GEOFENCE_POLYGON):
        d = math.sqrt((lat - p_lat)**2 + (lon - p_lon)**2)
        if d < min_dist:
            min_dist = d
            mejor_idx = i
    return GEOFENCE_POLYGON[mejor_idx]


# ============================================================================
# CLASE TrackerEmulado
# ============================================================================

class TrackerEmulado:
    def __init__(self, device_id, serial_number, nombre, es_toro=False, es_escapista=False):
        self.device_id = device_id
        self.serial_number = serial_number
        self.nombre = nombre
        self.campo = CAMPO_NOMBRE
        self.es_toro = es_toro
        self.es_escapista = es_escapista

        # Posicion inicial dentro del poligono
        self.lat, self.lon = generar_punto_dentro()

        # Parametros
        if es_toro:
            self.velocidad_max = random.uniform(4.5, 6.0)
            self.model = "TT-4G-PRO"
        else:
            self.velocidad_max = random.uniform(3.0, 4.5)
            self.model = random.choice(["TT-4G-PRO", "TT-4G-LITE"])

        self.firmware_version = "2.4.1" if self.model == "TT-4G-PRO" else "2.3.8"
        self.battery_level = random.uniform(75, 98)
        self.solar_panel = random.choice([True, False])
        self.network_operator = random.choice(["Movistar AR", "Claro AR"])

        # Estado del escapista
        self.fuera_del_geofence = False
        self.ciclos_en_estado = 0          # Ciclos que lleva dentro o fuera
        self.ciclos_objetivo = random.randint(5, 15)  # Ciclos antes de cambiar
        self.destino_lat = None
        self.destino_lon = None

        # Stats
        self.stats = {"enviados": 0, "exitosos": 0, "errores": 0}
        self.lock = threading.Lock()

    def mover(self):
        """Mueve el tracker segun su tipo"""
        if self.es_escapista:
            return self._mover_escapista()
        else:
            return self._mover_normal()

    def _mover_normal(self):
        """Movimiento normal dentro del poligono"""
        for _ in range(10):
            area = random.uniform(0.001, 0.003)
            nueva_lat = self.lat + random.uniform(-area, area)
            nueva_lon = self.lon + random.uniform(-area, area)
            if is_point_in_polygon(nueva_lat, nueva_lon, GEOFENCE_POLYGON):
                self.lat = nueva_lat
                self.lon = nueva_lon
                return random.uniform(0.5, 3.5)
        return 0.0

    def _mover_escapista(self):
        """Movimiento del tracker escapista: alterna entre dentro y fuera del geofence"""
        self.ciclos_en_estado += 1

        # Decidir si cambiar de estado (entrar/salir)
        if self.ciclos_en_estado >= self.ciclos_objetivo:
            self.ciclos_en_estado = 0

            if self.fuera_del_geofence:
                # Volver a entrar: elegir destino dentro del poligono
                self.fuera_del_geofence = False
                self.destino_lat, self.destino_lon = generar_punto_dentro()
                self.ciclos_objetivo = random.randint(8, 20)  # Permanecer dentro un rato
                print(f"  >>> {self.device_id} REGRESANDO al area de geofencing <<<")
            else:
                # Salir: elegir destino fuera del poligono
                self.fuera_del_geofence = True
                self.destino_lat, self.destino_lon = generar_punto_fuera()
                self.ciclos_objetivo = random.randint(5, 15)  # Permanecer fuera un rato
                print(f"  >>> {self.device_id} ESCAPANDO del area de geofencing <<<")

        # Moverse hacia el destino
        if self.destino_lat is not None:
            dir_lat = self.destino_lat - self.lat
            dir_lon = self.destino_lon - self.lon
            dist = math.sqrt(dir_lat**2 + dir_lon**2)

            if dist > 0.0005:  # Si no llego al destino
                # Moverse gradualmente (20-40% de la distancia por ciclo)
                factor = random.uniform(0.2, 0.4)
                self.lat += dir_lat * factor + random.uniform(-0.0003, 0.0003)
                self.lon += dir_lon * factor + random.uniform(-0.0003, 0.0003)
                return random.uniform(3.0, 6.0)  # Velocidad alta al escapar/regresar
            else:
                # Ya llego al destino, moverse aleatoriamente en la zona
                area = 0.002
                self.lat += random.uniform(-area, area)
                self.lon += random.uniform(-area, area)
                return random.uniform(0.5, 2.5)
        else:
            # Primera vez, establecer destino dentro
            self.destino_lat, self.destino_lon = generar_punto_dentro()
            return self._mover_normal()

    def generar_payload(self, velocidad):
        """Genera payload para la API"""
        cell = next((t for t in CELL_TOWERS if t["operator"] == self.network_operator), CELL_TOWERS[0])

        # Bateria
        hora_actual = datetime.now().hour
        if self.solar_panel and 9 <= hora_actual <= 18:
            self.battery_level = min(100, self.battery_level + random.uniform(0.1, 0.5))
        else:
            self.battery_level = max(0, self.battery_level - random.uniform(0, 0.08))

        activity_level = random.randint(60, 90) if self.es_escapista and self.fuera_del_geofence else random.randint(20, 60)

        return {
            "deviceId": self.device_id,
            "serialNumber": self.serial_number,
            "manufacturer": "TechTrack Argentina",
            "model": self.model,
            "firmwareVersion": self.firmware_version,
            "latitude": round(self.lat, 7),
            "longitude": round(self.lon, 7),
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "altitude": round(random.uniform(15, 35), 2),
            "speed": round(velocidad, 2),
            "heading": round(random.uniform(0, 360), 2),
            "speedAccuracy": round(random.uniform(0.5, 2.0), 2),
            "accuracy": round(random.uniform(3, 12), 2),
            "horizontalAccuracy": round(random.uniform(3, 10), 2),
            "verticalAccuracy": round(random.uniform(5, 15), 2),
            "hdop": round(random.uniform(0.8, 2.5), 1),
            "satellites": random.randint(7, 12),
            "gpsFixQuality": "3D",
            "batteryLevel": round(self.battery_level, 1),
            "batteryVoltage": round(3.7 + (self.battery_level / 100) * 0.5, 2),
            "chargingStatus": "solar" if self.solar_panel and 9 <= hora_actual <= 18 else "battery",
            "batteryTemperature": round(random.uniform(20, 40), 1),
            "signalStrength": random.randint(-85, -55),
            "networkType": "4G LTE",
            "operator": self.network_operator,
            "mcc": cell["mcc"],
            "mnc": cell["mnc"],
            "lac": cell["lac"],
            "cellId": cell["cellid"],
            "internalTemperature": round(random.uniform(25, 45), 1),
            "accelerometerX": round(random.uniform(-0.5, 0.5), 3),
            "accelerometerY": round(random.uniform(-0.5, 0.5), 3),
            "accelerometerZ": round(random.uniform(9.5, 10.2), 3),
            "activityLevel": activity_level,
            "transmissionInterval": INTERVALO_TRANSMISION,
            "transmissionPower": -10,
            "gpsModule": "Quectel L86",
            "modemModule": "Quectel EC25",
        }

    def enviar(self):
        """Mueve, genera payload y envia a la API"""
        velocidad = self.mover()
        payload = self.generar_payload(velocidad)

        with self.lock:
            self.stats["enviados"] += 1

        try:
            with limiter_lock:
                global last_api_call_time
                ahora = time.time()
                transcurrido = ahora - last_api_call_time
                if transcurrido < 0.5:
                    time.sleep(0.5 - transcurrido)
                last_api_call_time = time.time()

            response = session.post(API_URL, json=payload, timeout=10)
            tiempo_ms = response.elapsed.total_seconds() * 1000

            if response.status_code == 200:
                with self.lock:
                    self.stats["exitosos"] += 1
                return True, tiempo_ms, payload
            else:
                with self.lock:
                    self.stats["errores"] += 1
                return False, tiempo_ms, payload
        except Exception as e:
            with self.lock:
                self.stats["errores"] += 1
            return False, 0, payload


# ============================================================================
# FUNCIONES DE EJECUCION
# ============================================================================

def generar_trackers():
    """Genera trackers: 1 toro + N-1 vacas, con NUM_ESCAPISTAS escapistas"""
    trackers = []

    # Toro (nunca es escapista)
    toro = TrackerEmulado(
        device_id=TORO_DEVICE_ID,
        serial_number="GPS-ER-2024-BULL",
        nombre=f"Toro Pampa - {CAMPO_NOMBRE}",
        es_toro=True,
        es_escapista=False
    )
    trackers.append(toro)

    # Vacas
    for i in range(1, NUM_TRACKERS):
        vaca = TrackerEmulado(
            device_id=f"COW_GPS_ER_{i:03d}",
            serial_number=f"GPS-ER-2024-{i:03d}",
            nombre=f"Vaca {i:03d} - {CAMPO_NOMBRE}",
            es_toro=False,
            es_escapista=False
        )
        trackers.append(vaca)

    # Elegir escapistas aleatoriamente entre las vacas
    vacas = [t for t in trackers if not t.es_toro]
    escapistas = random.sample(vacas, min(NUM_ESCAPISTAS, len(vacas)))
    for esc in escapistas:
        esc.es_escapista = True
        # Desfasar los escapistas para que no salgan al mismo tiempo
        esc.ciclos_objetivo = random.randint(3, 10)

    return trackers, escapistas


def ejecutar(trackers, escapistas):
    """Ejecucion sincrona con log claro"""
    ciclo = 0
    total_enviados = 0
    total_exitosos = 0

    try:
        while True:
            ciclo += 1
            print(f"\n{'='*120}")
            print(f"  CICLO #{ciclo} - {datetime.now().strftime('%Y-%m-%d %H:%M:%S')} - FUERA DE LIMITES TEST")
            print(f"{'='*120}")

            # Estado de los escapistas
            for esc in escapistas:
                dentro = is_point_in_polygon(esc.lat, esc.lon, GEOFENCE_POLYGON)
                estado = "DENTRO" if dentro else "FUERA"
                color = "OK" if dentro else "!!"
                ciclos_restantes = esc.ciclos_objetivo - esc.ciclos_en_estado
                print(f"  [{color}] ESCAPISTA {esc.device_id} -> {estado} | "
                      f"Pos: ({esc.lat:.6f}, {esc.lon:.6f}) | "
                      f"Cambio en ~{ciclos_restantes} ciclos")

            print(f"  {'-'*110}")

            for tracker in trackers:
                exito, tiempo_ms, payload = tracker.enviar()
                total_enviados += 1
                if exito:
                    total_exitosos += 1

                esta_dentro = is_point_in_polygon(tracker.lat, tracker.lon, GEOFENCE_POLYGON)
                dentro_str = "OK" if esta_dentro else "FUERA"
                tipo_str = "TORO" if tracker.es_toro else ("ESC!" if tracker.es_escapista else "VACA")
                status_str = "OK" if exito else "ERR"

                print(f"  {status_str} {dentro_str:5} {tracker.device_id:18} {tipo_str:4} | "
                      f"GPS: ({payload['latitude']:9.6f}, {payload['longitude']:9.6f}) | "
                      f"Vel: {payload['speed']:5.1f} | Act: {payload['activityLevel']:3} | "
                      f"Bat: {payload['batteryLevel']:5.1f}% | {tiempo_ms:5.0f}ms")

                time.sleep(0.3)

            # Stats periodicas
            if ciclo % STATS_CADA_CICLOS == 0:
                tasa = (total_exitosos / total_enviados * 100) if total_enviados > 0 else 0
                fuera_count = sum(1 for t in trackers if not is_point_in_polygon(t.lat, t.lon, GEOFENCE_POLYGON))
                print(f"\n  {'='*110}")
                print(f"  STATS | Ciclo: {ciclo} | Enviados: {total_enviados} | "
                      f"Exitosos: {total_exitosos} | Tasa: {tasa:.1f}%")
                print(f"  GEOFENCE | Fuera del area: {fuera_count}/{len(trackers)}")
                for esc in escapistas:
                    dentro = is_point_in_polygon(esc.lat, esc.lon, GEOFENCE_POLYGON)
                    print(f"    Escapista {esc.device_id}: {'DENTRO' if dentro else 'FUERA'} "
                          f"({esc.lat:.6f}, {esc.lon:.6f})")
                print(f"  {'='*110}")

            print(f"\n  Esperando {INTERVALO_TRANSMISION} segundos...")
            time.sleep(INTERVALO_TRANSMISION)

    except KeyboardInterrupt:
        print(f"\n\n  Emulador detenido por el usuario")
        tasa = (total_exitosos / total_enviados * 100) if total_enviados > 0 else 0
        print(f"  Total: {total_enviados} enviados | {total_exitosos} exitosos | Tasa: {tasa:.1f}%")


# ============================================================================
# MAIN
# ============================================================================

def main():
    print("=" * 120)
    print("  EMULADOR GPS - TEST FUERA DE LIMITES (GEOFENCING)")
    print("=" * 120)
    print(f"  API: {API_URL}")
    print(f"  Trackers: {NUM_TRACKERS} (1 toro + {NUM_TRACKERS - 1} vacas)")
    print(f"  Escapistas: {NUM_ESCAPISTAS} vacas que entran y salen del geofence")
    print(f"  Campo: {CAMPO_NOMBRE}")
    print(f"  Geofencing: {len(GEOFENCE_POLYGON)} puntos")
    print(f"  Intervalo: {INTERVALO_TRANSMISION}s")
    print("=" * 120)

    trackers, escapistas = generar_trackers()

    print(f"\n  Trackers generados:")
    for t in trackers:
        tipo = "TORO" if t.es_toro else ("ESC!" if t.es_escapista else "VACA")
        print(f"    {t.device_id:18} {tipo:4} | ({t.lat:.6f}, {t.lon:.6f})")

    print(f"\n  Escapistas seleccionados:")
    for esc in escapistas:
        print(f"    {esc.device_id} - {esc.nombre}")
        print(f"      Primer cambio de estado en ~{esc.ciclos_objetivo} ciclos ({esc.ciclos_objetivo * INTERVALO_TRANSMISION}s)")

    print(f"\n{'='*120}")
    print("  Iniciando transmision... (Ctrl+C para detener)")
    print(f"{'='*120}\n")

    ejecutar(trackers, escapistas)


if __name__ == "__main__":
    main()
