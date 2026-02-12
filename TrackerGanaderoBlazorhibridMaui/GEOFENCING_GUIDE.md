# 🗺️ Guía de Geofencing - Tracker Ganadero

## 📱 Cómo usar la Gestión de Granjas y Geofencing

### 🚀 Acceso a la funcionalidad
- **Desde el menú lateral**: "🏡 Gestión de Granjas"
- **Desde el dashboard**: Botón "Gestión de Granjas" en acciones rápidas

### 🏗️ Crear una nueva granja
1. Haz clic en "Nueva Granja"
2. Completa los datos:
   - **Nombre**: Nombre de tu granja
   - **Descripción**: Descripción opcional
   - **Latitud/Longitud Central**: Punto central de la granja
3. Haz clic en "Guardar"

### 🗺️ Configurar Área de Geofencing

Tienes **3 opciones** para definir el área de geofencing:

#### 1️⃣ **Dibujo en Mapa (Recomendado)**
- Selecciona una granja de la lista
- Haz clic en "🖊️ Dibujar Área"
- Dibuja el polígono directamente en el mapa
- El área se actualiza automáticamente

#### 2️⃣ **Entrada Manual de Coordenadas**
- Ingresa latitud y longitud manualmente
- Haz clic en "Agregar Coordenada"
- Repite para cada punto del polígono
- Los puntos aparecen en la lista y en el mapa

#### 3️⃣ **Importación desde Archivo**
Soporta 3 formatos:

**CSV:**
```csv
latitude,longitude
-34.123456,-58.123456
-34.124456,-58.124456
-34.125456,-58.125456
```

**JSON:**
```json
[
  {"latitude": -34.123456, "longitude": -58.123456},
  {"latitude": -34.124456, "longitude": -58.124456},
  {"latitude": -34.125456, "longitude": -58.125456}
]
```

**KML:**
```xml
<coordinates>
-58.123456,-34.123456,0
-58.124456,-34.124456,0
-58.125456,-34.125456,0
</coordinates>
```

### ✅ Guardar y Gestionar
- **Guardar**: Haz clic en "💾 Guardar" para aplicar los cambios
- **Limpiar**: "🧹 Limpiar" para borrar todas las coordenadas
- **Editar**: Usa el botón "✏️" en la lista de granjas
- **Ver en mapa**: Usa el botón "🗺️" para visualizar
- **Eliminar**: Usa el botón "🗑️" (con confirmación)

### 🎯 Funcionalidades del Mapa
- **Zoom automático**: Se ajusta para mostrar toda el área
- **Edición en tiempo real**: Arrastra los puntos para ajustar
- **Vista híbrida**: Combina satélite y calles para mejor precisión
- **Marcadores visuales**: Diferentes colores según el estado

### 📱 Integración con el Sistema
- Las áreas configuradas se sincronizan con la API
- Se usan para detectar cuando los animales salen del área
- Generan alertas automáticas de geofencing
- Se visualizan en el "Mapa en Vivo"

### 💡 Consejos de Uso
1. **Precisión**: Usa al menos 4 puntos para definir un área
2. **Orden**: Los puntos se conectan en el orden que los agregas
3. **Tamaño de archivo**: Máximo 1MB para importaciones
4. **Backup**: Exporta tus coordenadas antes de hacer cambios importantes
5. **Pruebas**: Verifica el área en el "Mapa en Vivo" después de configurar

### 🔧 Resolución de Problemas
- **Mapa no carga**: Verifica la conexión a internet y la API key de Google Maps
- **Archivo no se importa**: Revisa el formato y que el archivo no esté corrupto
- **Área no se guarda**: Asegúrate de tener al menos 3 coordenadas
- **Coordenadas incorrectas**: Verifica que uses el formato decimal (no grados/minutos)

### 🌐 Formatos de Coordenadas
- **Formato correcto**: -34.123456, -58.123456 (decimal)
- **Formato incorrecto**: 34°7'24.4"S, 58°7'24.4"W (grados/minutos)
- **Rango válido**: Latitud (-90 a 90), Longitud (-180 a 180)