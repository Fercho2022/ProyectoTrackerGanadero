"""
Emulador GPS Stress Test - Trackers Ganaderos
Gualeguaychu, Entre Rios, Argentina

Emulador avanzado con:
- Escala configurable (15 a 200+ trackers)
- Intervalos de transmision variables por estado
- Simulacion de escenarios de alertas (aislamiento, dispersion, merodeo, celo)
- Threading para transmision concurrente
- Estadisticas detalladas de rendimiento
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
# CONFIGURACION - Modificar estos valores segun el modo deseado
# ============================================================================

MODO = "celo_test_1"
# Opciones: "demo", "normal", "escenarios", "stress_test", "apagado_gradual", "celo_test_1"

API_URL = "http://localhost:5192/api/gps/update"
CAMPO_NOMBRE = "La Esperanza"
TORO_DEVICE_ID = "BULL_GPS_ER_001"

# Presets por modo
PRESETS = {
    "demo": {
        "num_trackers": 15,
        "escenarios_activos": False,
        "intervalo_fijo": 10,       # Sincrono como el emulador original
        "usar_threading": False,
        "stats_cada_ciclos": 5,
    },
    "normal": {
        "num_trackers": 150,
        "escenarios_activos": False,
        "intervalo_fijo": None,     # Usa intervalos variables
        "usar_threading": True,
        "stats_cada_ciclos": 10,
    },
    "escenarios": {
        "num_trackers": 150,
        "escenarios_activos": True,
        "intervalo_fijo": None,
        "usar_threading": True,
        "stats_cada_ciclos": 10,
    },
    "stress_test": {
        "num_trackers": 200,
        "escenarios_activos": True,
        "intervalo_fijo": None,
        "usar_threading": True,
        "stats_cada_ciclos": 5,
        "intervalo_divisor": 2,     # Intervalos reducidos a la mitad
    },
    "apagado_gradual": {
        "num_trackers": 150,
        "escenarios_activos": False,
        "intervalo_fijo": None,
        "usar_threading": True,
        "stats_cada_ciclos": 10,
        "tiempo_normal_min": 5,     # Minutos de operacion normal antes de empezar a apagar
        "apagado_intervalo_seg": 30, # Segundos entre cada apagado de tracker
    },
    "celo_test_1": {
        "num_trackers": 150,
        "escenarios_activos": False,  # Controlado manualmente por celo_coordinator
        "intervalo_fijo": None,
        "usar_threading": True,
        "stats_cada_ciclos": 10,
        "fase_normal_min": 5,         # Minutos de actividad normal (baseline)
        "num_vacas_celo": 3,          # Cantidad de vacas que entran en celo
        "fase_celo_min": 10,          # Minutos de fase celo activo
    },
}

CONFIG = PRESETS[MODO]

# Intervalos de transmision por estado (segundos)
INTERVALOS_POR_ESTADO = {
    "pastando":    30,
    "descansando": 60,
    "caminando":   15,
    "alerta":      5,
    "aislado":     45,
    "merodeo":     10,
    "celo":        10,
}

# En stress_test, dividir intervalos
if MODO == "stress_test":
    divisor = CONFIG.get("intervalo_divisor", 2)
    INTERVALOS_POR_ESTADO = {k: max(2, v // divisor) for k, v in INTERVALOS_POR_ESTADO.items()}

# Probabilidades de escenarios (por ciclo de cada tracker)
PROB_AISLAMIENTO = 0.02
PROB_DISPERSION = 0.01    # Evaluado globalmente
PROB_MERODEO = 0.03
PROB_CELO = 0.01
MAX_VACAS_EN_CELO = 3

# Duraciones de escenarios (en ciclos del tracker)
DURACION_AISLAMIENTO = (20, 40)
DURACION_DISPERSION = (5, 10)
DURACION_MERODEO = (15, 30)
DURACION_CELO = (50, 100)

# ============================================================================
# POLIGONO DE GEOFENCING (34 puntos - Gualeguaychu)
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

# Estados base y sus caracteristicas de movimiento
ESTADOS_BASE = {
    "pastando":    {"velocidad_mult": 1.0, "movimiento_mult": 1.0, "prob": 0.60},
    "descansando": {"velocidad_mult": 0.1, "movimiento_mult": 0.2, "prob": 0.25},
    "caminando":   {"velocidad_mult": 1.5, "movimiento_mult": 1.3, "prob": 0.10},
    "alerta":      {"velocidad_mult": 2.0, "movimiento_mult": 1.8, "prob": 0.05},
}

# Torres celulares
CELL_TOWERS = [
    {"mcc": 722, "mnc": 70, "lac": 1234, "cellid": 56781, "operator": "Movistar AR"},
    {"mcc": 722, "mnc": 310, "lac": 1235, "cellid": 56782, "operator": "Claro AR"},
]

# ============================================================================
# CONFIGURACION DE RED (OPTIMIZACION DE PERFORMANCE)
# ============================================================================
# Usar Session con Connection Pooling para evitar agotar puertos/conexiones
session = requests.Session()
adapter = HTTPAdapter(pool_connections=200, pool_maxsize=200)
session.mount('http://', adapter)
session.mount('https://', adapter)


# ============================================================================
# RATE LIMITER GLOBAL
# ============================================================================
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


def generar_punto_aleatorio_dentro_poligono(max_intentos=100):
    """Genera un punto aleatorio dentro del poligono de geofencing"""
    for _ in range(max_intentos):
        lat = random.uniform(LAT_MIN, LAT_MAX)
        lon = random.uniform(LON_MIN, LON_MAX)
        if is_point_in_polygon(lat, lon, GEOFENCE_POLYGON):
            return lat, lon
    return GEOFENCE_POLYGON[len(GEOFENCE_POLYGON) // 2]


def calcular_centroide(trackers):
    """Calcula el centroide (promedio lat/lon) de una lista de trackers"""
    if not trackers:
        return (LAT_MIN + LAT_MAX) / 2, (LON_MIN + LON_MAX) / 2
    lat_sum = sum(t.lat for t in trackers)
    lon_sum = sum(t.lon for t in trackers)
    n = len(trackers)
    return lat_sum / n, lon_sum / n


def distancia_entre_puntos(lat1, lon1, lat2, lon2):
    """Distancia aproximada en metros entre dos puntos GPS"""
    dlat = (lat2 - lat1) * 111320
    dlon = (lon2 - lon1) * 111320 * math.cos(math.radians((lat1 + lat2) / 2))
    return math.sqrt(dlat**2 + dlon**2)


# ============================================================================
# CLASE TrackerEmulado
# ============================================================================

class TrackerEmulado:
    def __init__(self, device_id, serial_number, nombre, es_toro=False):
        self.device_id = device_id
        self.serial_number = serial_number
        self.nombre = nombre
        self.campo = CAMPO_NOMBRE
        self.es_toro = es_toro

        # Posicion inicial aleatoria dentro del poligono
        self.lat, self.lon = generar_punto_aleatorio_dentro_poligono()

        # Parametros individuales aleatorios
        if es_toro:
            self.velocidad_max = random.uniform(4.5, 6.0)
            self.area_movimiento = 0.004
            self.model = "TT-4G-PRO"
        else:
            self.velocidad_max = random.uniform(3.0, 4.5)
            self.area_movimiento = random.uniform(0.002, 0.004)
            self.model = random.choice(["TT-4G-PRO", "TT-4G-PRO", "TT-4G-LITE"])

        self.firmware_version = "2.4.1" if self.model == "TT-4G-PRO" else "2.3.8"
        self.hardware_version = "HW-3.2" if self.model == "TT-4G-PRO" else "HW-3.1"
        self.battery_capacity = 5000 if self.model == "TT-4G-PRO" else 4000
        self.battery_level = random.uniform(75, 98)
        self.solar_panel = random.choice([True, False])
        self.transmission_power = -10 if self.model == "TT-4G-PRO" else -12
        self.gps_module = "Quectel L86" if self.model == "TT-4G-PRO" else "Quectel L76"
        self.modem_module = "Quectel EC25" if self.model == "TT-4G-PRO" else "Quectel EC21"
        self.network_operator = random.choice(["Movistar AR", "Claro AR"])

        # Estado del animal
        self.estado = random.choices(
            ["pastando", "descansando", "caminando"],
            weights=[0.6, 0.25, 0.15]
        )[0]

        # Escenario activo
        self.escenario = None          # None, "aislado", "dispersion", "merodeo", "celo"
        self.escenario_ciclos = 0      # Ciclos restantes del escenario
        self.escenario_datos = {}      # Datos extra del escenario (eje merodeo, dir merodeo, etc)

        # Estadisticas individuales
        self.stats = {"enviados": 0, "exitosos": 0, "errores": 0, "rechazos_geofence": 0}
        self.tiempos_respuesta = []

        # Control de threading
        self.lock = threading.Lock()
        self.activo = True

    def obtener_intervalo(self):
        """Retorna el intervalo de transmision segun estado actual"""
        if CONFIG.get("intervalo_fijo"):
            return CONFIG["intervalo_fijo"]
        estado_key = self.escenario if self.escenario else self.estado
        return INTERVALOS_POR_ESTADO.get(estado_key, 30)

    def cambiar_estado_base(self):
        """Cambia el estado base aleatoriamente (si no tiene escenario activo)"""
        if self.escenario is not None:
            return
        if random.random() < 0.08:
            nuevo = random.choices(
                list(ESTADOS_BASE.keys()),
                weights=[ESTADOS_BASE[e]["prob"] for e in ESTADOS_BASE]
            )[0]
            if nuevo != self.estado:
                self.estado = nuevo

    def mover(self, centroide_lat, centroide_lon, toro=None):
        """Mueve el tracker segun su estado/escenario actual"""

        if self.escenario == "aislado":
            return self._mover_aislado(centroide_lat, centroide_lon)
        elif self.escenario == "dispersion":
            return self._mover_dispersion(centroide_lat, centroide_lon)
        elif self.escenario == "merodeo":
            return self._mover_merodeo()
        elif self.escenario == "celo":
            return self._mover_celo(toro)
        else:
            return self._mover_normal()

    def _mover_normal(self):
        """Movimiento normal segun estado base"""
        estado_config = ESTADOS_BASE.get(self.estado, ESTADOS_BASE["pastando"])
        lat_actual, lon_actual = self.lat, self.lon

        # Probabilidad de escape (generar alertas de geofencing)
        esta_dentro = is_point_in_polygon(lat_actual, lon_actual, GEOFENCE_POLYGON)
        prob_escape = 0.05 if self.estado == "alerta" else 0.01
        intentar_escape = random.random() < prob_escape

        # Si esta fuera, intentar volver
        if not esta_dentro and random.random() < 0.5:
            centro_lat = (LAT_MIN + LAT_MAX) / 2
            centro_lon = (LON_MIN + LON_MAX) / 2
            self.lat += (centro_lat - lat_actual) * 0.05
            self.lon += (centro_lon - lon_actual) * 0.05
            return random.uniform(2.0, 4.0)

        # Escape
        if intentar_escape and esta_dentro:
            velocidad = random.uniform(3.0, 6.0)
            area = self.area_movimiento * 2.5
            self.lat += random.uniform(-area, area)
            self.lon += random.uniform(-area, area)
            if not is_point_in_polygon(self.lat, self.lon, GEOFENCE_POLYGON):
                self.estado = "alerta"
            return velocidad

        # Movimiento normal dentro del poligono
        for _ in range(10):
            if self.estado == "descansando":
                velocidad = random.uniform(0, 0.5)
                lat_off = random.uniform(-0.0001, 0.0001)
                lon_off = random.uniform(-0.0001, 0.0001)
            else:
                velocidad = random.uniform(0, self.velocidad_max * estado_config["velocidad_mult"])
                area = self.area_movimiento * estado_config["movimiento_mult"]
                lat_off = random.uniform(-area, area)
                lon_off = random.uniform(-area, area)

            nueva_lat = lat_actual + lat_off
            nueva_lon = lon_actual + lon_off
            if is_point_in_polygon(nueva_lat, nueva_lon, GEOFENCE_POLYGON):
                self.lat = nueva_lat
                self.lon = nueva_lon
                return velocidad

        self.stats["rechazos_geofence"] += 1
        return 0.0

    def _mover_aislado(self, centroide_lat, centroide_lon):
        """Se aleja lentamente del centroide de la manada"""
        # Direccion opuesta al centroide
        dir_lat = self.lat - centroide_lat
        dir_lon = self.lon - centroide_lon
        dist = math.sqrt(dir_lat**2 + dir_lon**2)
        if dist > 0:
            dir_lat /= dist
            dir_lon /= dist

        # Movimiento lento en esa direccion + ruido
        paso = random.uniform(0.00005, 0.00015)
        nueva_lat = self.lat + dir_lat * paso + random.uniform(-0.00003, 0.00003)
        nueva_lon = self.lon + dir_lon * paso + random.uniform(-0.00003, 0.00003)

        if is_point_in_polygon(nueva_lat, nueva_lon, GEOFENCE_POLYGON):
            self.lat = nueva_lat
            self.lon = nueva_lon

        return random.uniform(0.1, 0.3)

    def _mover_dispersion(self, centroide_lat, centroide_lon):
        """Se mueve radialmente desde el centroide (panico)"""
        ciclos_restantes = self.escenario_ciclos
        ciclos_totales = self.escenario_datos.get("ciclos_totales", 10)

        # Primera mitad: dispersion. Segunda mitad: regreso
        if ciclos_restantes > ciclos_totales / 2:
            # Dispersion: alejarse del centroide
            dir_lat = self.lat - centroide_lat
            dir_lon = self.lon - centroide_lon
            dist = math.sqrt(dir_lat**2 + dir_lon**2)
            if dist > 0:
                dir_lat /= dist
                dir_lon /= dist

            paso = random.uniform(0.001, 0.003)
            nueva_lat = self.lat + dir_lat * paso + random.uniform(-0.0005, 0.0005)
            nueva_lon = self.lon + dir_lon * paso + random.uniform(-0.0005, 0.0005)
            velocidad = random.uniform(15, 25)
        else:
            # Regreso: acercarse al centroide
            dir_lat = centroide_lat - self.lat
            dir_lon = centroide_lon - self.lon
            dist = math.sqrt(dir_lat**2 + dir_lon**2)
            if dist > 0:
                dir_lat /= dist
                dir_lon /= dist

            paso = random.uniform(0.0005, 0.002)
            nueva_lat = self.lat + dir_lat * paso + random.uniform(-0.0003, 0.0003)
            nueva_lon = self.lon + dir_lon * paso + random.uniform(-0.0003, 0.0003)
            velocidad = random.uniform(5, 12)

        if is_point_in_polygon(nueva_lat, nueva_lon, GEOFENCE_POLYGON):
            self.lat = nueva_lat
            self.lon = nueva_lon

        return velocidad

    def _mover_merodeo(self):
        """Ida y vuelta en una linea recta (estres)"""
        eje = self.escenario_datos.get("eje", "NS")
        direccion = self.escenario_datos.get("direccion", 1)
        centro_lat = self.escenario_datos.get("centro_lat", self.lat)
        centro_lon = self.escenario_datos.get("centro_lon", self.lon)

        # ~200m de recorrido = ~0.0018 grados
        rango = 0.0009

        if eje == "NS":
            nueva_lat = self.lat + direccion * random.uniform(0.00015, 0.00025)
            nueva_lon = self.lon + random.uniform(-0.00002, 0.00002)
            # Cambiar direccion al llegar al extremo
            if abs(nueva_lat - centro_lat) > rango:
                self.escenario_datos["direccion"] = -direccion
        else:  # EW
            nueva_lat = self.lat + random.uniform(-0.00002, 0.00002)
            nueva_lon = self.lon + direccion * random.uniform(0.00015, 0.00025)
            if abs(nueva_lon - centro_lon) > rango:
                self.escenario_datos["direccion"] = -direccion

        if is_point_in_polygon(nueva_lat, nueva_lon, GEOFENCE_POLYGON):
            self.lat = nueva_lat
            self.lon = nueva_lon

        return random.uniform(3.0, 5.0)

    def _mover_celo(self, toro):
        """Se mueve con actividad elevada, acercandose gradualmente al toro"""
        if toro is None:
            return self._mover_normal()

        # Componente 1: movimiento hacia el toro (30% del paso)
        dir_lat = toro.lat - self.lat
        dir_lon = toro.lon - self.lon
        dist = math.sqrt(dir_lat**2 + dir_lon**2)
        if dist > 0:
            dir_lat /= dist
            dir_lon /= dist

        paso_toro = random.uniform(0.0002, 0.0006)

        # Componente 2: movimiento aleatorio amplificado (actividad alta)
        area = self.area_movimiento * 3
        lat_ruido = random.uniform(-area, area)
        lon_ruido = random.uniform(-area, area)

        nueva_lat = self.lat + dir_lat * paso_toro + lat_ruido
        nueva_lon = self.lon + dir_lon * paso_toro + lon_ruido

        if is_point_in_polygon(nueva_lat, nueva_lon, GEOFENCE_POLYGON):
            self.lat = nueva_lat
            self.lon = nueva_lon

        return random.uniform(self.velocidad_max, self.velocidad_max * 2)

    def generar_payload(self, velocidad):
        """Genera payload completo para enviar a la API"""
        cell = next((t for t in CELL_TOWERS if t["operator"] == self.network_operator), CELL_TOWERS[0])

        # Gestion de bateria
        hora_actual = datetime.now().hour
        if self.solar_panel and 9 <= hora_actual <= 18:
            self.battery_level = min(100, self.battery_level + random.uniform(0.1, 0.5))
        else:
            consumo = 0.15 if self.estado == "alerta" else 0.08
            self.battery_level = max(0, self.battery_level - random.uniform(0, consumo))

        satelites = random.randint(7, 12)
        hdop = round(random.uniform(0.8, 2.5), 1)

        # Activity level segun escenario
        if self.escenario == "aislado":
            activity_level = random.randint(5, 20)
        elif self.escenario == "celo":
            activity_level = random.randint(75, 100)
        elif self.escenario == "merodeo":
            activity_level = random.randint(60, 85)
        elif self.escenario == "dispersion":
            activity_level = random.randint(85, 100)
        elif self.estado == "descansando":
            activity_level = random.randint(5, 25)
        elif self.estado == "alerta":
            activity_level = random.randint(70, 95)
        else:
            activity_level = random.randint(30, 65)

        # Intervalo actual de transmision
        intervalo = self.obtener_intervalo()

        return {
            # Identificacion
            "deviceId": self.device_id,
            "serialNumber": self.serial_number,
            "manufacturer": "TechTrack Argentina",
            "model": self.model,
            "firmwareVersion": self.firmware_version,

            # GPS
            "latitude": round(self.lat, 7),
            "longitude": round(self.lon, 7),
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

            # Bateria
            "batteryLevel": round(self.battery_level, 1),
            "batteryVoltage": round(3.7 + (self.battery_level / 100) * 0.5, 2),
            "chargingStatus": "solar" if self.solar_panel and 9 <= hora_actual <= 18 else "battery",
            "batteryTemperature": round(random.uniform(20, 40), 1),

            # Red celular
            "signalStrength": random.randint(-85, -55),
            "networkType": "4G LTE",
            "operator": self.network_operator,
            "mcc": cell["mcc"],
            "mnc": cell["mnc"],
            "lac": cell["lac"],
            "cellId": cell["cellid"],

            # Sensores
            "internalTemperature": round(random.uniform(25, 45), 1),
            "accelerometerX": round(random.uniform(-0.5, 0.5), 3),
            "accelerometerY": round(random.uniform(-0.5, 0.5), 3),
            "accelerometerZ": round(random.uniform(9.5, 10.2), 3),

            # Activity
            "activityLevel": activity_level,

            # Configuracion
            "transmissionInterval": intervalo,
            "transmissionPower": self.transmission_power,
            "gpsModule": self.gps_module,
            "modemModule": self.modem_module,
        }

    def enviar(self, centroide_lat, centroide_lon, toro=None):
        """Mueve, genera payload y envia a la API"""
        velocidad = self.mover(centroide_lat, centroide_lon, toro)
        payload = self.generar_payload(velocidad)

        with self.lock:
            self.stats["enviados"] += 1

        try:
            # Rate limit a 1 request por segundo
            with limiter_lock:
                global last_api_call_time
                ahora = time.time()
                transcurrido = ahora - last_api_call_time
                if transcurrido < 1.0:
                    time.sleep(1.0 - transcurrido)
                last_api_call_time = time.time()

            # Usar la session global para reutilizar conexiones TCP (Keep-Alive)
            response = session.post(API_URL, json=payload, timeout=10)
            tiempo_ms = response.elapsed.total_seconds() * 1000

            with self.lock:
                self.tiempos_respuesta.append(tiempo_ms)
                if len(self.tiempos_respuesta) > 100:
                    self.tiempos_respuesta = self.tiempos_respuesta[-100:]

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
# FUNCIONES DE ESCENARIOS
# ============================================================================

def evaluar_escenarios_individuales(tracker, all_trackers, toro):
    """Evalua y activa escenarios individuales para un tracker"""
    if not CONFIG["escenarios_activos"]:
        return
    if tracker.es_toro:
        return
    if tracker.escenario is not None:
        # Decrementar ciclos
        tracker.escenario_ciclos -= 1
        if tracker.escenario_ciclos <= 0:
            estado_anterior = tracker.escenario
            tracker.escenario = None
            tracker.escenario_datos = {}
            tracker.estado = "pastando"
            return
        return

    # Evaluar si entra en algun escenario nuevo
    r = random.random()

    # Aislamiento
    if r < PROB_AISLAMIENTO:
        tracker.escenario = "aislado"
        tracker.escenario_ciclos = random.randint(*DURACION_AISLAMIENTO)
        tracker.escenario_datos = {}
        return

    # Merodeo
    if r < PROB_AISLAMIENTO + PROB_MERODEO:
        tracker.escenario = "merodeo"
        tracker.escenario_ciclos = random.randint(*DURACION_MERODEO)
        tracker.escenario_datos = {
            "eje": random.choice(["NS", "EW"]),
            "direccion": random.choice([1, -1]),
            "centro_lat": tracker.lat,
            "centro_lon": tracker.lon,
        }
        return

    # Celo (solo si hay toro y no se excede el maximo)
    if toro is not None and r < PROB_AISLAMIENTO + PROB_MERODEO + PROB_CELO:
        vacas_en_celo = sum(1 for t in all_trackers if t.escenario == "celo")
        if vacas_en_celo < MAX_VACAS_EN_CELO:
            tracker.escenario = "celo"
            tracker.escenario_ciclos = random.randint(*DURACION_CELO)
            tracker.escenario_datos = {}
            return


def evaluar_dispersion_global(all_trackers):
    """Evalua y activa dispersion de manada (evento global)"""
    if not CONFIG["escenarios_activos"]:
        return False

    # Solo si no hay dispersion en curso
    en_dispersion = any(t.escenario == "dispersion" for t in all_trackers)
    if en_dispersion:
        return False

    if random.random() < PROB_DISPERSION:
        ciclos = random.randint(*DURACION_DISPERSION)
        for t in all_trackers:
            if t.escenario is None:  # No interrumpir otros escenarios
                t.escenario = "dispersion"
                t.escenario_ciclos = ciclos
                t.escenario_datos = {"ciclos_totales": ciclos}
                t.estado = "alerta"
        return True
    return False


# ============================================================================
# ESTADISTICAS GLOBALES
# ============================================================================

class EstadisticasGlobales:
    def __init__(self):
        self.lock = threading.Lock()
        self.inicio = datetime.now()
        self.ciclos_coordinador = 0
        self.total_enviados = 0
        self.total_exitosos = 0
        self.total_errores = 0
        self.tiempos_respuesta = []
        self.requests_por_segundo = []
        self._ultimo_conteo = 0
        self._ultimo_tiempo = time.time()

    def registrar_envio(self, exito, tiempo_ms):
        with self.lock:
            self.total_enviados += 1
            if exito:
                self.total_exitosos += 1
            else:
                self.total_errores += 1
            if tiempo_ms > 0:
                self.tiempos_respuesta.append(tiempo_ms)
                if len(self.tiempos_respuesta) > 1000:
                    self.tiempos_respuesta = self.tiempos_respuesta[-1000:]

    def calcular_rps(self):
        with self.lock:
            ahora = time.time()
            elapsed = ahora - self._ultimo_tiempo
            if elapsed > 0:
                rps = (self.total_enviados - self._ultimo_conteo) / elapsed
                self._ultimo_conteo = self.total_enviados
                self._ultimo_tiempo = ahora
                self.requests_por_segundo.append(rps)
                if len(self.requests_por_segundo) > 100:
                    self.requests_por_segundo = self.requests_por_segundo[-100:]
                return rps
            return 0

    def mostrar(self, trackers):
        """Muestra reporte completo de estadisticas"""
        with self.lock:
            tiempo = (datetime.now() - self.inicio).total_seconds()
            mins, segs = int(tiempo // 60), int(tiempo % 60)
            tasa = (self.total_exitosos / self.total_enviados * 100) if self.total_enviados > 0 else 0

            # Tiempos de respuesta
            if self.tiempos_respuesta:
                t_min = min(self.tiempos_respuesta)
                t_max = max(self.tiempos_respuesta)
                t_avg = sum(self.tiempos_respuesta) / len(self.tiempos_respuesta)
            else:
                t_min = t_max = t_avg = 0

            # RPS
            rps = self.calcular_rps() if not self.requests_por_segundo else self.requests_por_segundo[-1]
            rps_max = max(self.requests_por_segundo) if self.requests_por_segundo else 0

        # Conteo por estado/escenario
        conteo_estados = defaultdict(int)
        trackers_fuera = 0
        for t in trackers:
            if t.escenario:
                conteo_estados[t.escenario] += 1
            else:
                conteo_estados[t.estado] += 1
            if not is_point_in_polygon(t.lat, t.lon, GEOFENCE_POLYGON):
                trackers_fuera += 1

        # Centroide
        cent_lat, cent_lon = calcular_centroide(trackers)

        # Distancia promedio al centroide
        distancias = [distancia_entre_puntos(t.lat, t.lon, cent_lat, cent_lon) for t in trackers]
        dist_avg = sum(distancias) / len(distancias) if distancias else 0
        dist_max = max(distancias) if distancias else 0

        print("\n" + "=" * 120)
        print(f"  ESTADISTICAS | Tiempo: {mins}m {segs}s | Modo: {MODO.upper()}")
        print("=" * 120)
        print(f"  Enviados: {self.total_enviados} | Exitosos: {self.total_exitosos} | "
              f"Errores: {self.total_errores} | Tasa exito: {tasa:.1f}%")
        print(f"  RPS actual: {rps:.1f} | RPS max: {rps_max:.1f} | "
              f"Respuesta API: min={t_min:.0f}ms avg={t_avg:.0f}ms max={t_max:.0f}ms")
        print(f"  Fuera del area: {trackers_fuera}/{len(trackers)} | "
              f"Distancia avg al centroide: {dist_avg:.0f}m | max: {dist_max:.0f}m")

        # Estados
        estado_str = " | ".join(f"{k}: {v}" for k, v in sorted(conteo_estados.items()))
        print(f"  Estados: {estado_str}")

        # Escenarios activos
        if CONFIG["escenarios_activos"]:
            aislados = [t for t in trackers if t.escenario == "aislado"]
            merodeando = [t for t in trackers if t.escenario == "merodeo"]
            en_celo = [t for t in trackers if t.escenario == "celo"]
            en_dispersion = any(t.escenario == "dispersion" for t in trackers)

            escenarios_str = []
            if aislados:
                escenarios_str.append(f"Aislados: {len(aislados)} ({', '.join(t.device_id for t in aislados)})")
            if merodeando:
                escenarios_str.append(f"Merodeo: {len(merodeando)} ({', '.join(t.device_id for t in merodeando)})")
            if en_celo:
                escenarios_str.append(f"Celo: {len(en_celo)} ({', '.join(t.device_id for t in en_celo)})")
            if en_dispersion:
                escenarios_str.append("DISPERSION ACTIVA")
            if escenarios_str:
                print(f"  Escenarios: {' | '.join(escenarios_str)}")
            else:
                print("  Escenarios: ninguno activo")

        print("=" * 120)


# ============================================================================
# FUNCIONES DE EJECUCION
# ============================================================================

def generar_trackers(num_trackers):
    """Genera la lista de trackers: 1 toro + N-1 vacas"""
    trackers = []

    # Toro
    toro = TrackerEmulado(
        device_id=TORO_DEVICE_ID,
        serial_number="GPS-ER-2024-BULL",
        nombre=f"Toro Pampa - {CAMPO_NOMBRE}",
        es_toro=True
    )
    trackers.append(toro)

    # Vacas
    for i in range(1, num_trackers):
        vaca = TrackerEmulado(
            device_id=f"COW_GPS_ER_{i:03d}",
            serial_number=f"GPS-ER-2024-{i:03d}",
            nombre=f"Vaca {i:03d} - {CAMPO_NOMBRE}",
            es_toro=False
        )
        trackers.append(vaca)

    return trackers


def worker_tracker(tracker, all_trackers, toro, stats_globales, stop_event):
    """Thread worker para un tracker individual"""
    # Offset aleatorio para escalonar el arranque
    offset = random.uniform(0, tracker.obtener_intervalo())
    time.sleep(offset)

    while not stop_event.is_set() and tracker.activo:
        # Calcular centroide actual
        cent_lat, cent_lon = calcular_centroide(all_trackers)

        # Evaluar escenarios individuales
        evaluar_escenarios_individuales(tracker, all_trackers, toro)

        # Cambiar estado base
        tracker.cambiar_estado_base()

        # Enviar datos
        exito, tiempo_ms, payload = tracker.enviar(cent_lat, cent_lon, toro)
        stats_globales.registrar_envio(exito, tiempo_ms)

        # Log compacto
        esta_dentro = is_point_in_polygon(tracker.lat, tracker.lon, GEOFENCE_POLYGON)
        dentro_str = "OK" if esta_dentro else "FUERA"
        escenario_str = f"[{tracker.escenario}]" if tracker.escenario else f"({tracker.estado})"
        status_str = "OK" if exito else "ERR"
        tipo_str = "TORO" if tracker.es_toro else "VACA"
        print(f"  {status_str} {dentro_str:5} {tracker.device_id:18} {tipo_str:4} {escenario_str:14} | "
              f"GPS: ({payload['latitude']:9.6f}, {payload['longitude']:9.6f}) | "
              f"Vel: {payload['speed']:5.1f} | Act: {payload['activityLevel']:3} | "
              f"Bat: {payload['batteryLevel']:5.1f}% | {tiempo_ms:5.0f}ms")

        # Esperar el intervalo correspondiente
        intervalo = tracker.obtener_intervalo()
        # Esperar en intervalos cortos para responder rapido al stop
        for _ in range(int(intervalo * 10)):
            if stop_event.is_set():
                return
            time.sleep(0.1)


def coordinador(all_trackers, toro, stats_globales, stop_event):
    """Thread coordinador: evalua escenarios globales y muestra stats"""
    ciclo = 0
    while not stop_event.is_set():
        time.sleep(5)  # Cada 5 segundos
        ciclo += 1
        stats_globales.ciclos_coordinador = ciclo

        # Evaluar dispersion global
        if evaluar_dispersion_global(all_trackers):
            print("\n  *** DISPERSION DE MANADA ACTIVADA ***\n")

        # Mostrar estadisticas periodicamente
        if ciclo % CONFIG["stats_cada_ciclos"] == 0:
            stats_globales.calcular_rps()
            stats_globales.mostrar(all_trackers)


def ejecutar_sincrono(trackers, toro, stats_globales):
    """Ejecucion sincrona (modo demo, compatible con emulador original)"""
    intervalo = CONFIG.get("intervalo_fijo", 10)
    ciclo = 0

    try:
        while True:
            ciclo += 1
            print(f"\n{'='*120}")
            print(f"  CICLO #{ciclo} - {datetime.now().strftime('%Y-%m-%d %H:%M:%S')} - Modo: {MODO.upper()}")
            print(f"{'='*120}")

            cent_lat, cent_lon = calcular_centroide(trackers)

            for tracker in trackers:
                if CONFIG["escenarios_activos"]:
                    evaluar_escenarios_individuales(tracker, trackers, toro)
                tracker.cambiar_estado_base()
                exito, tiempo_ms, payload = tracker.enviar(cent_lat, cent_lon, toro)
                stats_globales.registrar_envio(exito, tiempo_ms)

                esta_dentro = is_point_in_polygon(tracker.lat, tracker.lon, GEOFENCE_POLYGON)
                dentro_str = "OK" if esta_dentro else "FUERA"
                escenario_str = f"[{tracker.escenario}]" if tracker.escenario else f"({tracker.estado})"
                status_str = "OK" if exito else "ERR"
                tipo_str = "TORO" if tracker.es_toro else "VACA"
                print(f"  {status_str} {dentro_str:5} {tracker.device_id:18} {tipo_str:4} {escenario_str:14} | "
                      f"GPS: ({payload['latitude']:9.6f}, {payload['longitude']:9.6f}) | "
                      f"Vel: {payload['speed']:5.1f} | Act: {payload['activityLevel']:3} | "
                      f"Bat: {payload['batteryLevel']:5.1f}% | {tiempo_ms:5.0f}ms")
                time.sleep(0.3)

            # Evaluar dispersion global
            if CONFIG["escenarios_activos"] and evaluar_dispersion_global(trackers):
                print("\n  *** DISPERSION DE MANADA ACTIVADA ***\n")

            # Estadisticas
            if ciclo % CONFIG["stats_cada_ciclos"] == 0:
                stats_globales.calcular_rps()
                stats_globales.mostrar(trackers)

            print(f"\n  Esperando {intervalo} segundos...")
            time.sleep(intervalo)

    except KeyboardInterrupt:
        print("\n\n  Emulador detenido por el usuario")
        stats_globales.mostrar(trackers)


def shutdown_coordinator(trackers, toro, stop_event):
    """Thread que apaga trackers gradualmente despues de un tiempo de operacion normal"""
    tiempo_normal = CONFIG.get("tiempo_normal_min", 10)
    intervalo_apagado = CONFIG.get("apagado_intervalo_seg", 30)

    print(f"\n  [APAGADO GRADUAL] Operacion normal durante {tiempo_normal} minutos...")
    print(f"  [APAGADO GRADUAL] Luego se apagara 1 tracker cada {intervalo_apagado} segundos\n")

    # Esperar el tiempo de operacion normal
    for _ in range(int(tiempo_normal * 60 * 10)):
        if stop_event.is_set():
            return
        time.sleep(0.1)

    print(f"\n{'='*120}")
    print(f"  [APAGADO GRADUAL] Tiempo de operacion normal finalizado. Comenzando apagado gradual...")
    print(f"{'='*120}\n")

    # Obtener trackers que no son el toro (apagar vacas primero, toro al final)
    trackers_para_apagar = [t for t in trackers if not t.es_toro] + [t for t in trackers if t.es_toro]
    random.shuffle(trackers_para_apagar[:-1])  # Mezclar las vacas, toro queda ultimo

    apagados = 0
    for tracker in trackers_para_apagar:
        if stop_event.is_set():
            return

        tracker.activo = False
        apagados += 1
        activos = len(trackers) - apagados
        tipo = "TORO" if tracker.es_toro else "VACA"
        print(f"  [APAGADO] {tracker.device_id} ({tipo}) apagado | "
              f"Activos: {activos}/{len(trackers)} | "
              f"{datetime.now().strftime('%H:%M:%S')}")

        if activos == 0:
            print(f"\n{'='*120}")
            print(f"  [APAGADO GRADUAL] Todos los trackers apagados.")
            print(f"  [APAGADO GRADUAL] La API deberia detectar la caida masiva en ~5 minutos.")
            print(f"{'='*120}\n")
            return

        # Esperar antes de apagar el siguiente
        for _ in range(int(intervalo_apagado * 10)):
            if stop_event.is_set():
                return
            time.sleep(0.1)


def celo_coordinator(trackers, toro, stop_event):
    """
    Coordinador de test de celo (patron Gemini).
    Fase 1: Operacion normal → construye baseline de actividad
    Fase 2: Activa celo en N vacas → genera movimiento anomalo
    El backend detecta la anomalia y genera alerta PossibleHeat.
    """
    fase_normal = CONFIG.get("fase_normal_min", 5)
    num_vacas_celo = CONFIG.get("num_vacas_celo", 3)
    fase_celo = CONFIG.get("fase_celo_min", 10)
    api_base = API_URL.replace("/api/gps/update", "")

    print(f"\n{'='*120}")
    print(f"  [CELO TEST] Modo de prueba de deteccion de celo (patron Gemini)")
    print(f"  [CELO TEST] Fase 1: {fase_normal} min de operacion normal (baseline)")
    print(f"  [CELO TEST] Fase 2: {fase_celo} min con {num_vacas_celo} vacas en celo")
    print(f"{'='*120}\n")

    # Esperar 30 segundos para que los trackers envien sus primeros datos
    print(f"  [CELO TEST] Esperando 30s para inicio de transmision GPS...")
    for _ in range(300):
        if stop_event.is_set():
            return
        time.sleep(0.1)

    # === PASO 1: Inyectar baselines de prueba (7 dias de historial normal) ===
    print(f"\n  [CELO TEST] Inyectando 7 dias de baselines de prueba en la BD...")
    try:
        # Obtener farmId (asumimos farmId=1 para pruebas)
        resp = requests.post(f"{api_base}/api/breeding/seed-test-baselines/1", timeout=10)
        if resp.status_code == 200:
            data = resp.json()
            print(f"  [CELO TEST] OK: {data.get('recordsCreated', '?')} baselines creados "
                  f"para {data.get('animalsProcessed', '?')} animales")
        else:
            print(f"  [CELO TEST] ADVERTENCIA: seed-test-baselines retorno {resp.status_code}")
    except Exception as e:
        print(f"  [CELO TEST] ERROR al inyectar baselines: {e}")

    # === FASE 1: Operacion normal ===
    print(f"\n  [CELO TEST] === FASE 1: Operacion normal ({fase_normal} min) ===")
    for minuto in range(fase_normal):
        if stop_event.is_set():
            return
        restante = fase_normal - minuto
        print(f"  [CELO TEST] Fase 1 - {restante} min restantes | "
              f"Todas las vacas en movimiento normal | "
              f"{datetime.now().strftime('%H:%M:%S')}")
        for _ in range(600):  # 60 seconds
            if stop_event.is_set():
                return
            time.sleep(0.1)

    # === FASE 2: Activar celo ===
    print(f"\n{'='*120}")
    print(f"  [CELO TEST] === FASE 2: Activando celo en {num_vacas_celo} vacas ===")
    print(f"{'='*120}\n")

    # Seleccionar vacas aleatorias (no toro)
    vacas_disponibles = [t for t in trackers if not t.es_toro and t.activo]
    random.shuffle(vacas_disponibles)
    vacas_en_celo = vacas_disponibles[:num_vacas_celo]

    for vaca in vacas_en_celo:
        vaca.escenario = "celo"
        vaca.escenario_ciclos = 9999  # No se acaba hasta que termine la fase
        vaca.escenario_datos = {}
        print(f"  [CELO] {vaca.device_id} ({vaca.nombre}) → EN CELO | "
              f"Se acercara al toro y movimiento amplificado 3x")

    if toro:
        print(f"  [CELO] Toro: {toro.device_id} en ({toro.lat:.5f}, {toro.lon:.5f})")

    # Monitorear fase de celo
    for minuto in range(fase_celo):
        if stop_event.is_set():
            return
        restante = fase_celo - minuto

        # Mostrar distancia de cada vaca en celo al toro
        if toro:
            for vaca in vacas_en_celo:
                dist = distancia_entre_puntos(vaca.lat, vaca.lon, toro.lat, toro.lon)
                print(f"  [CELO] {vaca.device_id} → Distancia al toro: {dist:.0f}m | "
                      f"Pos: ({vaca.lat:.5f}, {vaca.lon:.5f})")

        print(f"  [CELO TEST] Fase 2 - {restante} min restantes | "
              f"{datetime.now().strftime('%H:%M:%S')}")

        # A mitad de la fase celo, forzar analisis
        if minuto == fase_celo // 2:
            print(f"\n  [CELO TEST] Forzando analisis de celo en el backend...")
            try:
                resp = requests.post(f"{api_base}/api/breeding/analyze-now/1", timeout=30)
                if resp.status_code == 200:
                    data = resp.json()
                    print(f"  [CELO TEST] Analisis completado: {data.get('animalsAnalyzed', '?')} "
                          f"animales analizados")
                else:
                    print(f"  [CELO TEST] ADVERTENCIA: analyze-now retorno {resp.status_code}")
            except Exception as e:
                print(f"  [CELO TEST] ERROR al forzar analisis: {e}")

        for _ in range(600):  # 60 seconds
            if stop_event.is_set():
                return
            time.sleep(0.1)

    # Forzar analisis final
    print(f"\n  [CELO TEST] Forzando analisis final de celo...")
    try:
        resp = requests.post(f"{api_base}/api/breeding/analyze-now/1", timeout=30)
        if resp.status_code == 200:
            data = resp.json()
            print(f"  [CELO TEST] Analisis final completado: {data.get('animalsAnalyzed', '?')} animales")
        else:
            print(f"  [CELO TEST] analyze-now retorno {resp.status_code}")
    except Exception as e:
        print(f"  [CELO TEST] ERROR: {e}")

    # Desactivar celo
    for vaca in vacas_en_celo:
        vaca.escenario = None
        vaca.escenario_ciclos = 0
        vaca.escenario_datos = {}

    print(f"\n{'='*120}")
    print(f"  [CELO TEST] Test de celo finalizado.")
    print(f"  [CELO TEST] Si las alertas fueron generadas, verificar en:")
    print(f"  [CELO TEST]   - Pagina de Alertas de la app")
    print(f"  [CELO TEST]   - Correo electronico ({num_vacas_celo} alertas esperadas)")
    print(f"  [CELO TEST] Los trackers continuan en operacion normal.")
    print(f"{'='*120}\n")


def ejecutar_threading(trackers, toro, stats_globales):
    """Ejecucion con threading (modos normal, escenarios, stress_test, apagado_gradual, celo_test_1)"""
    stop_event = threading.Event()

    extra_threads = 2 if MODO in ("apagado_gradual", "celo_test_1") else 1
    print(f"  Lanzando {len(trackers)} threads de trackers + {extra_threads} coordinador(es)...")

    try:
        with ThreadPoolExecutor(max_workers=len(trackers) + extra_threads) as executor:
            # Lanzar coordinador
            executor.submit(coordinador, trackers, toro, stats_globales, stop_event)

            # Lanzar coordinador de apagado gradual si corresponde
            if MODO == "apagado_gradual":
                executor.submit(shutdown_coordinator, trackers, toro, stop_event)

            # Lanzar coordinador de test de celo si corresponde
            if MODO == "celo_test_1":
                executor.submit(celo_coordinator, trackers, toro, stop_event)

            # Lanzar workers
            futures = []
            for tracker in trackers:
                f = executor.submit(worker_tracker, tracker, trackers, toro, stats_globales, stop_event)
                futures.append(f)

            # Esperar Ctrl+C
            while not stop_event.is_set():
                time.sleep(1)

    except KeyboardInterrupt:
        print("\n\n  Deteniendo emulador...")
        stop_event.set()
        time.sleep(2)
        stats_globales.mostrar(trackers)
        print("\n  Emulador finalizado\n")


# ============================================================================
# MAIN
# ============================================================================

def main():
    num_trackers = CONFIG["num_trackers"]

    print("=" * 120)
    print("  EMULADOR GPS STRESS TEST - TRACKER GANADERO")
    print("=" * 120)
    print(f"  API: {API_URL}")
    print(f"  Modo: {MODO.upper()}")
    print(f"  Trackers: {num_trackers} (1 toro + {num_trackers - 1} vacas)")
    print(f"  Campo: {CAMPO_NOMBRE}")
    print(f"  Geofencing: {len(GEOFENCE_POLYGON)} puntos")
    print(f"  Escenarios: {'ACTIVOS' if CONFIG['escenarios_activos'] else 'desactivados'}")
    print(f"  Threading: {'SI' if CONFIG['usar_threading'] else 'NO (sincrono)'}")
    if CONFIG.get("intervalo_fijo"):
        print(f"  Intervalo: {CONFIG['intervalo_fijo']}s fijo")
    else:
        print(f"  Intervalos: variables por estado ({min(INTERVALOS_POR_ESTADO.values())}s - {max(INTERVALOS_POR_ESTADO.values())}s)")
    if MODO == "apagado_gradual":
        print(f"  Apagado gradual: {CONFIG.get('tiempo_normal_min', 10)} min normal, luego 1 tracker cada {CONFIG.get('apagado_intervalo_seg', 30)}s")
    if MODO == "celo_test_1":
        print(f"  Test de celo: Fase 1 ({CONFIG.get('fase_normal_min', 5)} min normal) → "
              f"Fase 2 ({CONFIG.get('fase_celo_min', 10)} min con {CONFIG.get('num_vacas_celo', 3)} vacas en celo)")
        print(f"  Inyecta baselines + fuerza analisis automaticamente")
    print("=" * 120)

    # Generar trackers
    print(f"\n  Generando {num_trackers} trackers dentro del area de geofencing...")
    trackers = generar_trackers(num_trackers)
    toro = trackers[0]  # El primer tracker es el toro

    # Mostrar primeros trackers
    print(f"\n  Primeros trackers generados:")
    for t in trackers[:5]:
        tipo = "TORO" if t.es_toro else "VACA"
        solar = "Solar" if t.solar_panel else "Bateria"
        print(f"    {t.device_id:18} {tipo:4} | {t.model:12} | ({t.lat:.6f}, {t.lon:.6f}) | "
              f"{solar} {t.battery_level:.0f}% | {t.network_operator}")
    if len(trackers) > 5:
        print(f"    ... y {len(trackers) - 5} trackers mas")

    print(f"\n{'='*120}")
    print("  Iniciando transmision de datos GPS... (Ctrl+C para detener)")
    print(f"{'='*120}\n")

    stats_globales = EstadisticasGlobales()

    if CONFIG["usar_threading"]:
        ejecutar_threading(trackers, toro, stats_globales)
    else:
        ejecutar_sincrono(trackers, toro, stats_globales)


if __name__ == "__main__":
    main()
