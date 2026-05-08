# EvWeb Log Monitor - Contexto de Proyecto

Este archivo es el contexto vivo del proyecto de monitoreo de logs bajo `src/`.

Objetivo:
- Permitir que cualquier agente, chat nuevo o desarrollador entienda rapido como funciona el proyecto.
- Evitar tiempo perdido redescubriendo estructura, responsabilidades y flujo.
- Mantener una referencia unica cuando cambie el funcionamiento o la estructura.

Regla de mantenimiento:
- Actualizar este archivo cada vez que haya un cambio funcional importante.
- Actualizar este archivo cada vez que cambie la estructura de carpetas o archivos bajo `src/`.
- Si se agrega una dependencia, endpoint, capa, vista importante o flujo nuevo, documentarlo aca.

## 1. Resumen rapido

El proyecto bajo `src/` implementa una web MVC en .NET 8 para visualizar y descargar logs de tres aplicaciones de EvWeb:

- `Web`
- `Api`
- `Servicio`

La UI muestra un dashboard con:
- Tabs por aplicacion
- Selector de carpeta (`Errors`, `Info`, `Warnings`, `Legacy`)
- Selector de archivo
- Selector de fecha desde/hasta (aplicado a fecha de creacion de archivos disponibles)
- Selector de hora desde/hasta (aplicado a lineas del archivo, ignorando segundos)
- Selector de asociacion medica
- Selector de tipo de log
- Filtro por texto
- Selector de maximas lineas
- Lista de archivos disponibles
- Descarga del archivo seleccionado
- Visualizacion de lineas del log

Actualmente las tres fuentes usan el mismo path base:

`C:\Users\Stark\Desktop\Evweb\94469\Logs`

Dentro de esa ruta se esperan subcarpetas:
- `errors`
- `info`
- `warnings`

La opcion `Legacy` busca directamente en el path base, sin entrar a una subcarpeta.

## 2. Estado actual del comportamiento

### Dashboard dinamico

`Logs/Index` ya no depende de recargar la pagina completa para refrescar el visor del archivo.

Cuando cambia cualquiera de estos elementos:
- tab de fuente (`Web`, `Api`, `Servicio`)
- carpeta
- archivo
- fecha desde/hasta
- asociacion medica
- tipo de log
- texto de busqueda
- maximas lineas

el dashboard:
- hace una request AJAX via `fetch`
- resincroniza el formulario cuando cambia la fuente o la carpeta
- vuelve a renderizar solo la seccion del visor de logs para cambios comunes de archivo, texto o maximas lineas
- actualiza la URL del navegador para conservar el estado actual
- evita condiciones de carrera abortando requests anteriores si el usuario interactua rapido
- compara una firma de contenido y no reemplaza el DOM si el contenido visible final no cambio

Detalle importante del campo `Archivo`:
- cuando cambia `Web`, `Api`, `Servicio` o cambia la carpeta `Errors`, `Info`, `Warnings`, el servidor devuelve el estado nuevo del formulario
- eso permite repoblar correctamente el selector `Archivo`
- si una carpeta tiene un solo archivo, ese archivo debe aparecer igualmente como unica opcion seleccionable

Detalle importante del campo `Carpetas`:
- el dropdown ya no muestra siempre todas las carpetas posibles
- `Legacy` siempre aparece porque representa el path base de la fuente
- `Errors`, `Info` y `Warnings` solo aparecen si realmente existen para la fuente seleccionada
- otras carpetas fisicas que existan en el path base no se muestran ni se usan
- si la carpeta pedida en la URL no coincide con una opcion visible, el sistema vuelve a `Legacy`

Detalle importante de los nuevos filtros:
- `Fecha` ahora funciona con calendarios `Desde` y `Hasta`
- si ambos quedan vacios, equivale a `Todos`
- si se completa solo uno de los dos, filtra desde esa fecha o hasta esa fecha
- si ambos se completan con el mismo dia, equivale a una sola fecha
- si ambos se completan distintos, filtra por rango
- `Asociacion` siempre incluye `Todos` y se carga con las asociaciones detectadas en el archivo activo
- `Tipo de log` incluye `Todos`, `Errors`, `Warnings`, `Info` y `Sin tipo`
- si se cambia de archivo y la fecha o asociacion seleccionada ya no existe en ese archivo, el filtro se resetea automaticamente a `Todos`

### Descarga

La descarga del archivo sigue siendo tradicional via endpoint GET y no usa AJAX.

### Scroll infinito del visor

La seccion de lineas del archivo ahora tiene:
- scroll independiente del scroll general de la pagina
- carga incremental de lotes adicionales al llegar al final del scroll interno
- capacidad de seguir cargando hasta alcanzar la ultima linea disponible

El scroll infinito aplica solo dentro del panel de lineas y no sobre la pagina completa.

Las lineas visibles respetan el mismo orden en que fueron escritas en el archivo.

## 3. Version y stack

- Framework principal: `.NET 8` (`net8.0`)
- Tipo de app web: `ASP.NET Core MVC`
- UI: Razor Views
- JS cliente: JavaScript vanilla
- Estilos: CSS propio + Bootstrap base

## 4. Estructura general bajo src

```text
src/
  EvWeb.LogMonitor.CONTEXT.md
  EvWeb.LogMonitor.Application/
    Contracts/
      ILogDashboardService.cs
    Models/
      LogDashboardQuery.cs
      LogDashboardViewModel.cs
  EvWeb.LogMonitor.Domain/
    Constants/
      LogFolders.cs
    Entities/
      LogEntry.cs
      LogFileDescriptor.cs
      LogSourceDefinition.cs
  EvWeb.LogMonitor.Infrastructure/
    Configuration/
      LogMonitorOptions.cs
    Services/
      FileSystemLogDashboardService.cs
    DependencyInjection.cs
  EvWeb.LogMonitor.Web/
    Controllers/
      LogsController.cs
    Views/
      Logs/
        Index.cshtml
        _DashboardContent.cshtml
        _LogViewer.cshtml
        _LogLineBatch.cshtml
      Shared/
        _Layout.cshtml
      _ViewImports.cshtml
      _ViewStart.cshtml
    wwwroot/
      css/
        site.css
      js/
        site.js
    appsettings.json
    Program.cs
```

## 5. Arquitectura por capas

El proyecto esta separado en cuatro capas:

### 5.1 Domain

Ubicacion:
- `src/EvWeb.LogMonitor.Domain`

Responsabilidad:
- Contener modelos simples y reglas basicas compartidas.
- No depende de infraestructura ni de MVC.

Archivos clave:
- `Constants/LogFolders.cs`
  - Define `errors`, `info`, `warnings`, `legacy`
  - Normaliza nombres de carpeta
  - Valida si una carpeta es admitida
  - Expone nombre visible para UI
  - Permite resolver si una carpeta usa el path base directamente
- `Entities/LogEntry.cs`
  - Representa una linea parseada del log
- `Entities/LogFileDescriptor.cs`
  - Representa archivo, fecha de creacion y fecha de modificacion
- `Entities/LogSourceDefinition.cs`
  - Representa una fuente como `Web`, `Api` o `Servicio`

### 5.2 Application

Ubicacion:
- `src/EvWeb.LogMonitor.Application`

Responsabilidad:
- Definir contratos y modelos del caso de uso.
- No sabe leer archivos ni renderizar HTML.

Archivos clave:
- `Contracts/ILogDashboardService.cs`
  - Contrato principal para obtener datos del dashboard y descarga
- `Models/LogDashboardQuery.cs`
  - Input del caso de uso
  - Campos:
    - `Source`
    - `Folder`
    - `FileName`
    - `Text`
    - `DateFrom`
    - `DateTo`
    - `Association`
    - `LogType`
    - `MaxLines`
    - `Offset`
- `Models/LogDashboardViewModel.cs`
  - Output completo que consume la vista
  - Incluye lineas, archivos, fuentes, asociaciones disponibles, rango de fechas seleccionado, carpeta seleccionada, archivo seleccionado, mensajes de error, etc.

### 5.3 Infrastructure

Ubicacion:
- `src/EvWeb.LogMonitor.Infrastructure`

Responsabilidad:
- Implementar el caso de uso contra el sistema de archivos.
- Resolver configuracion y DI.

Archivos clave:
- `Configuration/LogMonitorOptions.cs`
  - Modelo de configuracion para leer `LogMonitor:Sources` desde `appsettings.json`
- `DependencyInjection.cs`
  - Registra `ILogDashboardService` -> `FileSystemLogDashboardService`
- `Services/FileSystemLogDashboardService.cs`
  - Servicio principal
  - Lee carpetas y archivos
  - Valida rutas
  - Lee archivos con `FileShare.ReadWrite`
  - Pagina lotes de lineas respetando el orden real del archivo
  - Filtra por texto
  - Filtra por fecha
  - Filtra por asociacion medica
  - Filtra por tipo de log
  - Parsea lineas del log
  - Devuelve stream seguro para descarga

### 5.4 Web

Ubicacion:
- `src/EvWeb.LogMonitor.Web`

Responsabilidad:
- Host MVC
- Routing
- Configuracion web
- Razor views
- JS del dashboard

Archivos clave:
- `Program.cs`
  - Configura MVC
  - Registra infraestructura
  - Define `Logs/Index` como ruta por defecto
- `appsettings.json`
  - Configura fuentes de logs
- `Controllers/LogsController.cs`
  - Endpoint de pagina principal
  - Endpoint parcial AJAX
  - Endpoint de lotes para scroll infinito
  - Endpoint de descarga
- `Views/Logs/Index.cshtml`
  - Solo contenedor raiz del dashboard
- `Views/Logs/_DashboardContent.cshtml`
  - Renderiza la estructura estatica del dashboard
- `Views/Logs/_LogViewer.cshtml`
  - Renderiza solo el visor dinamico del archivo abierto
- `Views/Logs/_LogLineBatch.cshtml`
  - Renderiza un lote de filas para el scroll infinito del visor
- `Views/Shared/_Layout.cshtml`
  - Layout general de la aplicacion
- `wwwroot/js/site.js`
  - Logica de actualizacion dinamica
- `wwwroot/css/site.css`
  - Estilos del dashboard

## 6. Flujo funcional completo

### 6.1 Carga inicial

1. El usuario entra a `Logs/Index`.
2. `LogsController.Index(...)` recibe query string.
3. El controller llama a `BuildViewModelAsync(...)`.
4. Eso delega en `ILogDashboardService`.
5. La implementacion concreta es `FileSystemLogDashboardService`.
6. El servicio arma `LogDashboardViewModel`.
7. `Index.cshtml` renderiza el contenedor raiz.
8. `Index.cshtml` incluye `_DashboardContent.cshtml`, que dibuja la estructura base y monta el visor.

### 6.2 Refresco dinamico del dashboard

1. El usuario cambia un filtro o hace click en un tab/chip de archivo.
2. `site.js` intercepta el evento.
3. `site.js` construye la URL con el estado actual del formulario.
4. Hace `fetch` al endpoint `Logs/Dashboard`.
5. Si cambia fuente o carpeta, el servidor devuelve el partial `_DashboardContent` y el JS resincroniza hero, tabs y formulario.
6. Si cambia archivo, tambien se resincronizan las opciones de asociacion porque dependen del archivo activo.
7. El visor sigue actualizandose de manera aislada, reusando solo la parte dinamica necesaria.
8. Se actualiza la URL visible del navegador sin recargar completamente.
9. Si la firma del contenido visible no cambia, no se reemplaza el DOM.

### 6.3 Scroll infinito del visor

1. El visor carga inicialmente el primer lote segun `MaxLines`, respetando el orden natural del archivo.
2. Las lineas viven dentro de un contenedor con scroll propio.
3. Al acercarse al final del scroll interno, `site.js` llama a `Logs/LinesBatch`.
4. El servidor devuelve el siguiente lote de lineas en el partial `_LogLineBatch`.
5. El JS agrega esas filas al final de la lista visible.
6. El proceso se repite hasta que no queden mas lineas por cargar.

### 6.4 Descarga de archivo

1. El usuario toca `Descargar`.
2. Se llama `LogsController.Download(...)`.
3. El servicio valida que el archivo pertenezca a la carpeta permitida.
4. Devuelve `FileStream` con `FileShare.ReadWrite`.
5. ASP.NET devuelve el archivo al navegador.

## 7. Endpoints actuales

### GET `/Logs/Index`

Uso:
- Render completo de la pagina

Parametros:
- `source`
- `folder`
- `file`
- `dateFrom`
- `dateTo`
- `association`
- `logType`
- `text`
- `maxLines`

Respuesta:
- View completa `Index`

### GET `/Logs/Dashboard`

Uso:
- Refresco parcial AJAX del dashboard

Parametros:
- `source`
- `folder`
- `file`
- `dateFrom`
- `dateTo`
- `association`
- `logType`
- `text`
- `maxLines`

Respuesta:
- Partial view `_DashboardContent`

### GET `/Logs/LinesBatch`

Uso:
- Cargar siguiente lote del scroll infinito del visor

Parametros:
- `source`
- `folder`
- `file`
- `dateFrom`
- `dateTo`
- `association`
- `logType`
- `text`
- `maxLines`
- `offset`

Respuesta:
- Partial view `_LogLineBatch`

### GET `/Logs/Download`

Uso:
- Descargar archivo seleccionado

Parametros:
- `source`
- `folder`
- `file`

Respuesta:
- Archivo de texto

## 8. Como se resuelven las fuentes y carpetas

Configuracion actual en `EvWeb.LogMonitor.Web/appsettings.json`:

- `Web` -> `C:\Users\Stark\Desktop\Evweb\94469\Logs`
- `Api` -> `C:\Users\Stark\Desktop\Evweb\94469\Logs`
- `Servicio` -> `C:\Users\Stark\Desktop\Evweb\94469\Logs`

Internamente, el servicio toma:
- path base de la fuente
- carpeta seleccionada (`errors`, `info`, `warnings`, `legacy`)

y construye:

`Path.Combine(sourcePath, folder)`

Ejemplos:
- `...\Logs\errors`
- `...\Logs\info`
- `...\Logs\warnings`

Caso especial:
- `legacy` usa directamente `sourcePath`

## 9. Como funciona el parser de logs

El parser esta en `FileSystemLogDashboardService.ParseLine(...)`.

Intenta parsear dos formatos:

### Formato 1: estilo Serilog

Ejemplo conceptual:

`2026-04-30 12:10:29.000 -03:00 [WRN] Mensaje`

Extrae:
- timestamp
- categoria/nivel
- mensaje

### Formato 2: estilo bracket visto en logs reales

Ejemplo real aproximado:

`[ General | 11:55:09.7463428] Type:Process: ...`

Extrae:
- categoria -> `General`
- timestamp -> `11:55:09.7463428`
- mensaje -> resto de la linea

Normalizacion visual actual:
- el tipo de log se intenta extraer desde la propia linea
- prioridad principal: token tipo `Type:...`
- tambien se normalizan formatos abreviados como `ERR`, `WRN` e `INF`
- caso especial actual: `Type:Process` se interpreta como `Info`
- valores visibles admitidos: `Errors`, `Warnings`, `Info`
- si una linea no trae tipo reconocible, no recibe ninguna etiqueta

Normalizacion de fecha/hora actual:
- la columna `Fecha/Hora` del visor se obtiene prioritariamente desde la propia linea del log
- en formatos tipo `[ asociacion | fechaHora ] mensaje`, se toma la parte `fechaHora`
- si la parte izquierda del bracket corresponde a una asociacion, esa asociacion se guarda aparte para el filtro `Asociacion`
- si la linea trae fecha y hora completa, se formatea
- si la linea trae solo una hora o un texto temporal parcial, se conserva ese valor de la linea
- si la linea no trae timestamp reconocible, la columna queda vacia

Filtrado por tipo de log:
- se apoya en la misma normalizacion usada por las etiquetas visuales
- `Sin tipo` muestra solo lineas sin tipo reconocible
- el filtrado no altera el orden del archivo: solo descarta lineas y conserva el orden original de las restantes

### Fallback

Si la linea no coincide con ningun formato:
- se guarda completa como mensaje
- timestamp = `--`
- categoria = `General`

## 10. Seguridad y validaciones

### Path traversal

Para evitar que un request lea archivos fuera de la carpeta permitida:
- se usa `Path.GetFileName(fileName)` para limpiar el nombre
- se arma la ruta final con `Path.GetFullPath(...)`
- se compara contra la carpeta base permitida

Si la ruta resultante no queda dentro de la carpeta esperada:
- el archivo se rechaza

### Lectura concurrente

Los archivos se abren con:
- `FileAccess.Read`
- `FileShare.ReadWrite`

Esto permite leerlos aunque otra aplicacion los este escribiendo.

## 11. JS cliente: comportamiento importante

Archivo:
- `src/EvWeb.LogMonitor.Web/wwwroot/js/site.js`

Responsabilidades:
- Inicializar dashboard
- Interceptar submit del formulario
- Escuchar `input` y `change`
- Debounce en el campo de texto
- Hacer fetch parcial
- Manejar scroll infinito interno del visor
- Reemplazar contenido renderizado
- Comparar firma de contenido para evitar updates visuales inutiles
- Mantener URL sincronizada
- Abortar request anterior si llega una nueva
- Soportar `popstate` para navegacion del navegador

Detalles importantes:
- El buscador de texto usa debounce de `250ms`
- Si el usuario escribe rapido, requests viejas se abortan
- Si una respuesta vieja llega tarde, no pisa el contenido mas nuevo
- Si el contenido final visible no cambia, el visor no se vuelve a renderizar
- El scroll infinito carga lotes adicionales dentro del scroll propio del listado de lineas
- Cuando cambia `source` o `folder`, tambien se resincroniza el estado del formulario para que el selector `Archivo` refleje exactamente los archivos disponibles en esa combinacion
- Cuando cambia `file`, tambien se resincronizan `Fecha` y `Asociacion` porque sus opciones dependen del archivo cargado
- El filtro `Tipo de log` viaja tambien en las requests parciales y en el scroll infinito

## 12. CSS y layout

Archivo principal:
- `src/EvWeb.LogMonitor.Web/wwwroot/css/site.css`

Define:
- shell general
- hero superior
- tabs de fuente
- panel de filtros
- panel de logs
- chips de archivos
- estado de carga del visor
- highlight suave por fila segun categoria visible (`Info`, `Errors`, `Warning`)
- etiqueta por linea coloreada segun categoria visible (`Info`, `Errors`, `Warning`)
- layout compacto de tres columnas en el visor: fecha/hora, categoria y mensaje
- densidad visual general reducida en header, hero, tabs, filtros y chips para priorizar el espacio del visor
- densidad visual general reducida aun mas en elementos externos al visor para maximizar el espacio util del archivo cargado
- la columna `Fecha/Hora` del visor se alimenta desde el timestamp parseado de cada linea cuando existe
- contenedor de lineas con scroll propio e infinito por lotes

Estado de carga:
- el contenedor con `data-log-viewer-container` recibe clase `is-loading`
- solo el visor muestra overlay liviano durante la consulta

## 13. Archivos mas importantes para tocar segun el tipo de cambio

### Si cambia la logica de lectura de archivos
- `Infrastructure/Services/FileSystemLogDashboardService.cs`

### Si cambia la forma del modelo que consume la vista
- `Application/Models/LogDashboardViewModel.cs`
- `Application/Models/LogDashboardQuery.cs`

### Si cambia la configuracion de fuentes
- `Web/appsettings.json`
- `Infrastructure/Configuration/LogMonitorOptions.cs`

### Si cambia el flujo HTTP
- `Web/Controllers/LogsController.cs`
- `Web/Program.cs`

### Si cambia solo la UI del dashboard
- `Web/Views/Logs/_DashboardContent.cshtml`
- `Web/Views/Logs/_LogViewer.cshtml`
- `Web/Views/Logs/_LogLineBatch.cshtml`
- `Web/wwwroot/css/site.css`
- `Web/wwwroot/js/site.js`

### Si cambia el layout global
- `Web/Views/Shared/_Layout.cshtml`

## 14. Convenciones actuales del dashboard

- Tabs de fuentes: `Web`, `Api`, `Servicio`
- Carpetas visibles: `Errors`, `Info`, `Warnings`, `Legacy`
- Carpetas reales en disco: `errors`, `info`, `warnings`, `legacy`
- Filtro `Fecha`: calendarios `Desde` y `Hasta` sobre fecha de creacion del archivo
- Filtro `Hora`: campos `Desde` y `Hasta` sobre la hora de cada linea (sin segundos)
- Filtro `Asociacion`: dropdown con opcion `Todos`
- Filtro `Tipo de log`: dropdown con `Todos`, `Errors`, `Warnings`, `Info`, `Sin tipo`
- Ruta por defecto de la app: `Logs/Index`
- La vista principal es parcializable
- La estructura del dashboard vive en `_DashboardContent.cshtml`
- El contenido dinamico real del visor vive en `_LogViewer.cshtml`
- Los lotes adicionales del scroll infinito viven en `_LogLineBatch.cshtml`

## 15. Compilacion y verificacion

Proyecto web:
- `src/EvWeb.LogMonitor.Web/EvWeb.LogMonitor.Web.csproj`

Solucion principal actual:
- `Maintenance.SVC.sln`

Nota operativa importante:
- Si `dotnet build` falla con errores de copia de DLL en `EvWeb.LogMonitor.Web`, normalmente significa que hay una instancia del proyecto web ejecutandose y bloqueando binarios.
- En ese caso, detener el proceso que este usando `EvWeb.LogMonitor.Web.dll` y volver a compilar.

## 16. Limitaciones actuales

- Las tres fuentes usan el mismo path base por ahora.
- La lectura es contra archivos locales del sistema, no contra base de datos ni storage remoto.
- El filtro de texto se aplica sobre el archivo completo antes de paginar los lotes del visor.
- No hay SignalR ni streaming real de logs; el comportamiento actual es "reactivo por cambio de filtros", no push server-side en vivo.

## 17. Proximos cambios probables

Cambios que probablemente se pidan mas adelante y donde impactarian:

- Diferente `LogPath` por fuente
  - `Web/appsettings.json`
- Auto refresh periodico
  - `Web/wwwroot/js/site.js`
- Colores/etiquetas por nivel real del log
  - parser en `Infrastructure`
  - render en `_DashboardContent.cshtml`
- Paginacion o virtualizacion de lineas
  - `Application`, `Infrastructure`, `Web`
- Endpoint API JSON en lugar de partial HTML
  - `LogsController`
  - `site.js`
- Optimizacion del scroll infinito con virtualizacion real
  - `Application`, `Infrastructure`, `Web`

## 18. Checklist para futuros agentes antes de tocar algo

1. Leer este archivo completo.
2. Confirmar si el cambio es:
   - estructura
   - logica de lectura
   - UI
   - configuracion
3. Tocar la capa correcta y no mezclar responsabilidades.
4. Si se altera comportamiento o estructura, actualizar este archivo.
5. Si el build falla, revisar si hay un proceso de `EvWeb.LogMonitor.Web` corriendo.

## 19. Ultima actualizacion importante

Fecha de referencia:
- `2026-05-08`

Cambio documentado:
- Se refino el flujo AJAX para que solo actualice el visor del archivo usando el endpoint `Logs/Dashboard`, el partial `_LogViewer.cshtml` y una firma de contenido para evitar reemplazos visuales innecesarios.
- Las lineas del visor ahora normalizan `General` o categoria vacia a `Info`, `Errors` o `Warning` segun la carpeta seleccionada.
- Las filas del visor ahora aplican un highlight suave alineado con la categoria visible para mejorar escaneo sin afectar la lectura.
- La UI general fue compactada para que el mayor espacio util de la pantalla quede disponible para las lineas del log.
- Los elementos externos al visor fueron compactados aun mas, sin reducir la seccion que muestra el archivo.
- El visor ahora tiene scroll independiente e infinito por lotes hasta alcanzar la ultima linea disponible.
- El endpoint `Logs/Dashboard` ahora devuelve `_DashboardContent` para poder resincronizar tabs, hero y formulario cuando cambia la fuente o la carpeta, corrigiendo especialmente el selector `Archivo`.
- La carpeta `Legacy` ahora busca archivos directamente en el path base de cada fuente.
- El visor ahora mantiene el mismo orden de lineas que trae el archivo y toma `Fecha/Hora` desde el timestamp de cada linea parseada, dejando `Sin archivos disponibles` cuando no hay logs para seleccionar.
- Se agregaron los filtros `Fecha` y `Asociacion`, ambos con opcion `Todos`, y la asociacion se extrae del bloque izquierdo en formatos `[ asociacion | fechaHora ]`.
- La etiqueta visual `Legacy` se removio del encabezado de referencias y las lineas sin fecha detectable ahora dejan la columna `Fecha/Hora` vacia.
- El visor ahora muestra una columna `Asociacion` por fila, con `N/A` cuando la linea no trae una asociacion, y el filtro de fechas paso a funcionar con un rango `Desde/Hasta` basado en calendarios.
- El tipo de log ahora se detecta desde cada linea del archivo y ya no se deduce por carpeta; ademas se agrego el filtro `Tipo de log` manteniendo siempre el orden original del archivo.
- El desplegable `Carpetas` ahora se arma dinamicamente segun las carpetas que realmente existan para cada fuente.
- `Legacy` quedo como opcion fija del desplegable `Carpetas`, mientras que `Errors`, `Warnings` e `Info` solo aparecen si existen fisicamente.
- La estetica general de la web fue rediseñada para alinearse visualmente con el backoffice de referencia: topbar blanca con iconografia Bootstrap, fondo gris claro, tabs estilo Bootstrap, paneles tipo card y visor de logs con look mas sobrio de backoffice, sin cambiar funcionalidad.
