"""
Script para crear animales de forma masiva en Tracker Ganadero
Genera más de 100 animales con datos variados y realistas
"""

import requests
import random
from datetime import datetime, timedelta
import time

# ============================================================================
# CONFIGURACIÓN
# ============================================================================

API_URL = "http://localhost:5192/api/Animals"
FARM_ID = 1  # ID de Granja Norte (ajustar si es diferente)
CANTIDAD_ANIMALES = 120  # Cantidad de animales a crear

# Nombres argentinos de vacas y toros
NOMBRES_MACHOS = [
    "Pampa", "Gaucho", "Toro", "Macho", "Bravo", "Rojo", "Negro", "Blanco",
    "Fortín", "Potro", "Relincho", "Bandido", "Caudillo", "Rebelde", "Centauro",
    "Chacarero", "Pulpero", "Tropero", "Resero", "Jinete", "Domador", "Palenque",
    "Facón", "Lazo", "Boleador", "Mancarrón", "Overo", "Zaino", "Moro", "Tordillo"
]

NOMBRES_HEMBRAS = [
    "Lola", "Manchada", "Pegada", "Tristes", "Cafe", "Luna", "Estrella", "Aurora",
    "Flor", "Rosa", "Margarita", "Violeta", "Azucena", "Dalia", "Hortensia",
    "Mimosa", "Amapola", "Gardenia", "Jazmín", "Magnolia", "Petunia", "Begonia",
    "Caléndula", "Camelia", "Clavel", "Delfina", "Gladiola", "Iris", "Jacinta",
    "Lila", "Orquídea", "Primavera", "Tulipán", "Verbena", "Zinnia", "Alegría"
]

PREFIJOS_DESCRIPTIVOS = [
    "Ojo", "Pinta", "Cara", "Pata", "Cola", "Oreja", "Frente", "Lomo",
    "Costado", "Cuerno", "Hocico", "Pecho", "Anca", "Paleta", "Corvejón"
]

# Razas ganaderas argentinas comunes
RAZAS = [
    "Angus",
    "Brangus",
    "Hereford",
    "Braford",
    "Shorthorn",
    "Charolais",
    "Limousin",
    "Holando Argentino",
    "Jersey",
    "Criollo"
]

# Estados de salud
ESTADOS = [
    "Saludable",
    "Saludable",
    "Saludable",
    "Saludable",  # Mayoría saludables
    "En tratamiento",
    "Recuperación",
    "Gestante"
]

# ============================================================================
# FUNCIONES AUXILIARES
# ============================================================================

def generar_nombre_animal(genero, contador):
    """Genera un nombre único para el animal"""
    if genero == "Male":
        if contador % 3 == 0:
            return random.choice(NOMBRES_MACHOS)
        else:
            prefijo = random.choice(PREFIJOS_DESCRIPTIVOS)
            sufijo = random.choice(NOMBRES_MACHOS)
            return f"{prefijo}{sufijo}"
    else:  # Female
        if contador % 3 == 0:
            return random.choice(NOMBRES_HEMBRAS)
        else:
            prefijo = random.choice(PREFIJOS_DESCRIPTIVOS)
            sufijo = random.choice(NOMBRES_HEMBRAS)
            return f"{prefijo}{sufijo}"

def generar_tag(contador):
    """Genera un tag/número único para el animal"""
    # Formato: Rebo#### (como los que ya existen)
    return f"Rebo{1001 + contador:04d}"

def generar_fecha_nacimiento():
    """Genera una fecha de nacimiento aleatoria (entre 6 meses y 5 años)"""
    dias_atras = random.randint(180, 1825)  # 6 meses a 5 años
    fecha = datetime.now() - timedelta(days=dias_atras)
    return fecha.strftime("%Y-%m-%dT%H:%M:%S")

def generar_peso(genero, edad_dias):
    """Genera un peso realista según género y edad"""
    edad_meses = edad_dias / 30

    if genero == "Male":
        # Toros: 300-800 kg según edad
        if edad_meses < 12:
            peso_base = 200 + (edad_meses * 15)
        else:
            peso_base = 400 + ((edad_meses - 12) * 8)
        peso_base = min(peso_base, 800)
    else:  # Female
        # Vacas: 250-600 kg según edad
        if edad_meses < 12:
            peso_base = 180 + (edad_meses * 12)
        else:
            peso_base = 350 + ((edad_meses - 12) * 5)
        peso_base = min(peso_base, 600)

    # Agregar variación aleatoria ±10%
    variacion = random.uniform(0.9, 1.1)
    peso = peso_base * variacion

    return round(peso, 1)

def calcular_edad_dias(fecha_nacimiento_str):
    """Calcula la edad en días desde una fecha"""
    fecha_nac = datetime.fromisoformat(fecha_nacimiento_str.replace('Z', '+00:00'))
    edad = datetime.now() - fecha_nac
    return edad.days

# ============================================================================
# FUNCIÓN PRINCIPAL
# ============================================================================

def verificar_api():
    """Verifica que la API esté funcionando"""
    print("\n🔍 Verificando conexión con la API...")
    try:
        # Intentar hacer un GET a la API base
        base_url = API_URL.rsplit('/', 1)[0]  # Obtener URL base
        response = requests.get(base_url, timeout=5)
        print(f"✅ API respondiendo (Status: {response.status_code})")
        return True
    except requests.exceptions.ConnectionError:
        print("❌ ERROR: No se puede conectar a la API")
        print(f"   Asegúrate de que la API esté corriendo en {API_URL}")
        print("   Ejecuta: cd ProyectoApiWebTrackerGanadero && dotnet run")
        return False
    except Exception as e:
        print(f"❌ ERROR al conectar: {e}")
        return False

def crear_animales_masivo():
    """Crea múltiples animales de forma automática"""

    print("=" * 80)
    print("  CREACIÓN MASIVA DE ANIMALES - TRACKER GANADERO")
    print("=" * 80)
    print(f"  API: {API_URL}")
    print(f"  Granja ID: {FARM_ID}")
    print(f"  Cantidad a crear: {CANTIDAD_ANIMALES}")
    print("=" * 80)

    # Verificar que la API esté corriendo
    if not verificar_api():
        return

    print()

    exitos = 0
    errores = 0
    animales_creados = []

    # Distribución de géneros (60% hembras, 40% machos)
    generos = (["Female"] * 60) + (["Male"] * 40)
    random.shuffle(generos)

    for i in range(CANTIDAD_ANIMALES):
        try:
            # Seleccionar género
            genero = generos[i % len(generos)]

            # Generar datos del animal
            fecha_nacimiento = generar_fecha_nacimiento()
            edad_dias = calcular_edad_dias(fecha_nacimiento)

            animal_data = {
                "name": generar_nombre_animal(genero, i),
                "tag": generar_tag(i),
                "birthDate": fecha_nacimiento,
                "gender": genero,
                "breed": random.choice(RAZAS),
                "weight": generar_peso(genero, edad_dias),
                "status": random.choice(ESTADOS),
                "farmId": FARM_ID
            }

            # Enviar request a la API
            response = requests.post(API_URL, json=animal_data, timeout=10)

            if response.status_code in [200, 201]:
                exitos += 1
                animal_creado = response.json()
                animales_creados.append(animal_data)

                print(f"✅ [{exitos:3d}/{CANTIDAD_ANIMALES}] {animal_data['name']:20s} | "
                      f"{animal_data['tag']:12s} | {animal_data['breed']:18s} | "
                      f"{'🐂' if genero == 'Male' else '🐄'} {genero:6s} | "
                      f"{animal_data['weight']:6.1f} kg")
            else:
                errores += 1
                error_msg = response.text if len(response.text) < 200 else response.text[:200] + "..."
                print(f"❌ [{i+1:3d}] Error {response.status_code}")
                print(f"    Datos enviados: {animal_data}")
                print(f"    Respuesta: {error_msg}")

                # Detener después del primer error para diagnóstico
                if errores == 1:
                    print("\n⚠️  DETENIENDO después del primer error para diagnóstico.")
                    print("    Revisa el error arriba y corrige antes de continuar.\n")
                    break

            # Pequeña pausa para no saturar la API
            time.sleep(0.1)

        except Exception as e:
            errores += 1
            print(f"❌ [{i+1:3d}] Excepción: {str(e)}")

    # Resumen final
    print()
    print("=" * 80)
    print("  RESUMEN")
    print("=" * 80)
    print(f"  ✅ Creados exitosamente: {exitos}")
    print(f"  ❌ Errores: {errores}")
    print(f"  📊 Tasa de éxito: {(exitos/CANTIDAD_ANIMALES*100):.1f}%")

    if animales_creados:
        machos = sum(1 for a in animales_creados if a['gender'] == 'Male')
        hembras = sum(1 for a in animales_creados if a['gender'] == 'Female')
        print(f"  🐂 Machos: {machos}")
        print(f"  🐄 Hembras: {hembras}")

        razas_count = {}
        for a in animales_creados:
            raza = a['breed']
            razas_count[raza] = razas_count.get(raza, 0) + 1

        print(f"\n  Distribución por raza:")
        for raza, count in sorted(razas_count.items(), key=lambda x: x[1], reverse=True):
            print(f"    {raza:18s}: {count:3d} animales")

    print("=" * 80)
    print()

# ============================================================================
# EJECUCIÓN
# ============================================================================

if __name__ == "__main__":
    try:
        print("\n¿Está seguro de que desea crear {} animales? (s/n): ".format(CANTIDAD_ANIMALES), end='')
        confirmacion = input().strip().lower()

        if confirmacion == 's' or confirmacion == 'si':
            crear_animales_masivo()
        else:
            print("\nOperación cancelada por el usuario.")
    except KeyboardInterrupt:
        print("\n\nOperación interrumpida por el usuario.")
    except Exception as e:
        print(f"\n\nError fatal: {e}")
