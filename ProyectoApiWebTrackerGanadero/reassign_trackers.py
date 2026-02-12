#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Script para reasignar trackers existentes a los animales GPS-ER correctos
y disociar trackers de animales incorrectos
"""

import requests
import json

API_BASE_URL = "http://localhost:5192/api"

def get_animals():
    """Obtiene todos los animales"""
    try:
        response = requests.get(f"{API_BASE_URL}/Animals")
        response.raise_for_status()
        return response.json()
    except Exception as e:
        print(f"Error obteniendo animales: {e}")
        return []

def get_trackers():
    """Obtiene todos los trackers"""
    try:
        response = requests.get(f"{API_BASE_URL}/Trackers")
        response.raise_for_status()
        return response.json()
    except Exception as e:
        print(f"Error obteniendo trackers: {e}")
        return []

def update_animal_tracker(animal_id, tracker_id):
    """Actualiza el tracker de un animal"""
    try:
        # Obtener datos actuales del animal
        response = requests.get(f"{API_BASE_URL}/Animals/{animal_id}")
        response.raise_for_status()
        animal_data = response.json()

        # Actualizar solo el trackerId
        update_data = {
            "name": animal_data["name"],
            "tag": animal_data["tag"],
            "birthDate": animal_data["birthDate"],
            "gender": animal_data["gender"],
            "breed": animal_data["breed"],
            "weight": animal_data["weight"],
            "status": animal_data["status"],
            "trackerId": tracker_id
        }

        response = requests.put(f"{API_BASE_URL}/Animals/{animal_id}",
                              json=update_data,
                              headers={"Content-Type": "application/json"})
        response.raise_for_status()
        return True
    except Exception as e:
        print(f"Error actualizando animal {animal_id}: {e}")
        if hasattr(e, 'response') and e.response is not None:
            print(f"Response: {e.response.text}")
        return False

def main():
    print("REASIGNACION DE TRACKERS GPS ENTRE RIOS")
    print("=" * 60)

    # Obtener todos los animales y trackers
    animals = get_animals()
    trackers = get_trackers()

    # Filtrar animales GPS-ER de la granja 7
    gps_er_animals = [a for a in animals if a['farmId'] == 7 and a['tag'].startswith('GPS-ER-')]
    gps_er_animals.sort(key=lambda x: x['tag'])

    # Filtrar trackers COW_GPS_ER
    gps_er_trackers = [t for t in trackers if t['deviceId'].startswith('COW_GPS_ER_')]
    gps_er_trackers.sort(key=lambda x: x['deviceId'])

    print(f"Animales GPS-ER encontrados: {len(gps_er_animals)}")
    print(f"Trackers COW_GPS_ER encontrados: {len(gps_er_trackers)}")

    # Primero, disociar trackers de animales incorrectos
    print(f"\nPASO 1: Disociando trackers de animales incorrectos")
    print("-" * 60)

    other_farm_animals = [a for a in animals if a['farmId'] == 8 and a['trackerId'] is not None]
    for animal in other_farm_animals:
        tracker_id = animal['trackerId']
        # Verificar si este tracker debería estar en un animal GPS-ER
        tracker = next((t for t in trackers if t['id'] == tracker_id), None)
        if tracker and tracker['deviceId'].startswith('COW_GPS_ER_'):
            print(f"Disociando tracker {tracker_id} ({tracker['deviceId']}) del animal {animal['tag']} (Farm {animal['farmId']})")
            if update_animal_tracker(animal['id'], None):
                print(f"  OK - Tracker disociado")
            else:
                print(f"  ERROR - Fallo la disociacion")

    print(f"\nPASO 2: Asociando trackers con animales GPS-ER correctos")
    print("-" * 60)

    # Mapear device IDs a números
    for animal in gps_er_animals:
        tag = animal['tag']
        animal_id = animal['id']

        # Extraer número del tag (GPS-ER-001 -> 1, GPS-ER-002 -> 2, etc.)
        if tag.startswith('GPS-ER-'):
            try:
                tag_number = int(tag.split('-')[-1])
                expected_device_id = f"COW_GPS_ER_{tag_number:02d}"

                # Buscar el tracker correspondiente
                tracker = next((t for t in gps_er_trackers if t['deviceId'] == expected_device_id), None)

                if tracker:
                    tracker_id = tracker['id']
                    current_tracker = animal['trackerId']

                    if current_tracker != tracker_id:
                        print(f"Asociando {tag} (Animal {animal_id}) -> Tracker {tracker_id} ({expected_device_id})")
                        if update_animal_tracker(animal_id, tracker_id):
                            print(f"  OK - Asociacion completada")
                        else:
                            print(f"  ERROR - Fallo la asociacion")
                    else:
                        print(f"YA ASOCIADO: {tag} -> Tracker {tracker_id} ({expected_device_id})")
                else:
                    print(f"TRACKER NO ENCONTRADO: {tag} -> {expected_device_id}")

            except ValueError:
                print(f"ERROR: Tag invalido {tag}")

    print(f"\n" + "=" * 60)
    print("REASIGNACION COMPLETADA")

if __name__ == "__main__":
    main()