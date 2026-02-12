# 🐄 Tracker Ganadero - Aplicación Blazor Hybrid MAUI

Una aplicación móvil moderna desarrollada con .NET MAUI y Blazor para el monitoreo y gestión integral de ganado en tiempo real.

## 📱 Características Principales

### 🗺️ Localización en Tiempo Real
- **Mapa en vivo** con integración de Google Maps API
- **Tracking GPS** de animales con actualización automática
- **Geofencing** para alertas de salida de zonas seguras
- **Historial de rutas** y movimientos

### ⚠️ Sistema de Alertas Inteligente
- **Detección automática** de actividad anormal
- **Alertas de salud** basadas en patrones de comportamiento
- **Notificaciones push** en tiempo real
- **Gestión de alertas** con sistema de resolución

### 🐮 Gestión de Animales
- **Inventario completo** con información detallada
- **Control de peso** y seguimiento de engorde
- **Registro genealógico** y reproducción
- **Estados de salud** y monitoreo

### 🏥 Control Sanitario
- **Registros veterinarios** completos
- **Historial de vacunaciones** y tratamientos
- **Programación de revisiones** médicas
- **Control de medicamentos** y costos

### 📊 Reportes y Análisis
- **Dashboard ejecutivo** con métricas clave
- **Análisis de productividad** por animal y granja
- **Reportes financieros** de compras y ventas
- **Uso de pasturas** y rotación

### 💰 Gestión Financiera
- **Control de transacciones** de compra/venta
- **Seguimiento de costos** operativos
- **Análisis de rentabilidad** por animal
- **Facturación** y registros contables

## 🛠️ Tecnologías Utilizadas

### Frontend
- **.NET MAUI** - Framework multiplataforma
- **Blazor Hybrid** - UI moderna y responsiva
- **Bootstrap 5** - Framework CSS
- **Font Awesome** - Iconografía
- **JavaScript/HTML5** - Interactividad web

### Backend
- **.NET 8** Web API
- **PostgreSQL** con extensiones PostGIS
- **Entity Framework Core** - ORM
- **SignalR** - Comunicación en tiempo real
- **AutoMapper** - Mapeo de objetos

### Servicios Externos
- **Google Maps API** - Mapas y geolocalización
- **SignalR Hub** - Comunicación bidireccional
- **JWT Authentication** - Seguridad

## 🏗️ Arquitectura de la Aplicación

```
TrackerGanaderoBlazorHibridMaui/
├── Models/                 # Modelos de datos (DTOs)
├── Services/              # Servicios de comunicación con API
├── Pages/                 # Páginas Blazor
│   ├── Auth/             # Autenticación
│   ├── Dashboard/        # Páginas principales
│   ├── Animals/          # Gestión de animales
│   ├── Alerts/           # Sistema de alertas
│   ├── Reports/          # Reportes y análisis
│   ├── Health/           # Control sanitario
│   └── Inventory/        # Inventario y finanzas
├── Shared/               # Componentes compartidos
├── wwwroot/              # Recursos estáticos
└── Platforms/            # Configuración por plataforma
```

## 🚀 Instalación y Configuración

### Requisitos Previos
- Visual Studio 2022 (v17.8 o superior)
- .NET 8 SDK
- Workload de .NET MAUI instalado
- Android SDK (para desarrollo Android)
- Xcode (para desarrollo iOS - solo en Mac)

### Configuración
1. **Clonar el repositorio**
   ```bash
   git clone [url-del-repositorio]
   cd TrackerGanaderoBlazorHibridMaui
   ```

2. **Configurar la API**
   - Asegúrate de que la API web esté ejecutándose en `https://localhost:7028`
   - Verifica la conexión a PostgreSQL con PostGIS

3. **Configurar Google Maps**
   - La API Key ya está configurada en `MauiProgram.cs`
   - Para producción, mover a configuración segura

4. **Restaurar paquetes**
   ```bash
   dotnet restore
   ```

5. **Compilar y ejecutar**
   ```bash
   dotnet build
   dotnet run
   ```

## 📱 Funcionalidades por Página

### 🏠 Dashboard Principal
- Vista general del estado del ganado
- Métricas en tiempo real
- Accesos rápidos a funciones principales
- Estado de conectividad SignalR

### 🗺️ Mapa en Vivo
- Visualización de todos los animales en tiempo real
- Filtros por granja
- Información detallada por animal
- Estado de trackers GPS

### 🐄 Gestión de Animales
- Lista completa de animales
- Búsqueda y filtros avanzados
- Formularios de alta/edición
- Historial de peso y crecimiento

### ⚠️ Sistema de Alertas
- Alertas activas y resueltas
- Filtros por prioridad y tipo
- Resolución de alertas
- Notificaciones automáticas

### 📋 Control Sanitario
- Registros veterinarios
- Historial de tratamientos
- Programación de vacunas
- Control de costos médicos

### 📊 Reportes
- Estadísticas de productividad
- Análisis de peso y crecimiento
- Comparativas por granja
- Exportación de datos

### 💼 Inventario y Finanzas
- Registro de compras y ventas
- Control de inventario
- Análisis financiero
- Seguimiento de transacciones

## 🔧 Configuración Avanzada

### SignalR en Tiempo Real
```csharp
// Configuración automática en MauiProgram.cs
builder.Services.AddSingleton<SignalRService>();
```

### Autenticación JWT
```csharp
// Almacenamiento seguro automático
await SecureStorage.SetAsync("auth_token", token);
```

### Google Maps Integration
```javascript
// Configuración en LiveMap.razor
initializeMap("AIzaSyAjKR-ToKKA54K2TSdrTPtyOPhQkjwxrHE");
```

## 🎨 Personalización de UI

### Temas y Colores
- Modificar `wwwroot/css/app.css` para cambios visuales
- Paleta de colores personalizable
- Soporte para modo claro/oscuro

### Responsive Design
- Optimizado para móviles y tablets
- Navegación adaptativa
- Componentes responsivos

## 🔒 Seguridad

### Autenticación
- JWT Tokens con expiración automática
- Almacenamiento seguro con SecureStorage
- Renovación automática de tokens

### Comunicación
- HTTPS en todas las comunicaciones
- Validación de certificados SSL
- Encriptación de datos sensibles

## 📋 Dependencias Principales

```xml
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
<PackageReference Include="System.Text.Json" Version="8.0.0" />
```

## 🐛 Solución de Problemas

### Problemas Comunes

1. **Error de conexión a la API**
   - Verificar que la API esté ejecutándose
   - Comprobar la URL base en `MauiProgram.cs`

2. **Mapa no se carga**
   - Verificar la API Key de Google Maps
   - Comprobar conectividad a internet

3. **SignalR desconectado**
   - Verificar el token de autenticación
   - Comprobar la configuración del hub

## 🚀 Deployment

### Android
```bash
dotnet publish -f net8.0-android -c Release
```

### iOS
```bash
dotnet publish -f net8.0-ios -c Release
```

### Windows
```bash
dotnet publish -f net8.0-windows10.0.19041.0 -c Release
```

## 📈 Roadmap

### Próximas Funcionalidades
- [ ] Integración con IoT sensors
- [ ] Machine Learning para predicciones
- [ ] Modo offline con sincronización
- [ ] Notificaciones push nativas
- [ ] Integración con drones
- [ ] API para terceros

## 👥 Contribución

Para contribuir al proyecto:
1. Fork del repositorio
2. Crear branch para la feature
3. Commit de cambios
4. Push al branch
5. Crear Pull Request

## 📄 Licencia

Este proyecto está bajo la licencia MIT. Ver `LICENSE` para más detalles.

## 📞 Soporte

Para soporte técnico o consultas:
- Email: soporte@trackerganadero.com
- Issues: GitHub Issues
- Documentación: Wiki del proyecto

---

**Tracker Ganadero** - Tecnología al servicio de la ganadería moderna 🐄