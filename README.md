# Addon Corroborador CAE

## Arquitectura
Este proyecto está compuesto de dos partes fundamentales que aseguran la máxima integridad y desacoplamiento:
- **Frontend (SAP Business One Add-on)**: Construido en `.NET Framework 4.8`, su única responsabilidad es extraer los datos dinámicos desde la UI de SAP, realizar comprobaciones básicas de integridad, y llamar al backend usando el cliente nativo `HttpClient`.
- **Backend (ASP.NET Core Web API)**: Construido en `.NET 5.0`, centraliza de forma segura la autenticación con AFIP, el mapeo lógico de tipos de comprobante, el uso del CUIT Representante y el firmado criptográfico.

Toda documentación de progreso y contexto de decisiones técnicas se guarda en la carpeta `memory-bank` en la raíz del proyecto.

## Configuración y Permisos en IIS (Producción)

### Requerimiento Importante: Permisos de Carpeta
Para el correcto funcionamiento de la API en el servidor con IIS (WebApiCAE), es **obligatorio** que el usuario del pool de aplicaciones (típicamente `IIS AppPool\CorroboradorCAE`) tenga permisos de **Escritura** (Write) y **Lectura** (Read) sobre las siguientes carpetas dentro del directorio de publicación del backend:

1. **`Logs`**: Utilizada por Serilog para generar el archivo de registro `error-.txt`. Si el usuario no tiene permisos, la aplicación podría fallar o no registrar los errores.
2. **`ResourcesAFIP`**: Utilizada para acceder y gestionar los certificados de AFIP (.p12) para autenticación y firma. 

### ¿Cómo otorgar los permisos en el servidor?
1. Haz clic derecho sobre la carpeta afectada (`Logs` o `ResourcesAFIP`).
2. Selecciona **Propiedades** > Pestaña **Seguridad**.
3. Haz clic en **Editar...** > **Agregar...**.
4. En la ventana emergente, escribe `IIS AppPool\CorroboradorCAE` (reemplaza 'CorroboradorCAE' con el nombre exacto de tu Application Pool si es distinto).
   *Nota: Si estás buscando en el dominio o grupo, asegúrate de que 'Ubicaciones' esté apuntando a la máquina local.*
5. Haz clic en **Aceptar**.
6. Con el usuario recién agregado seleccionado, marca la casilla **Control total** o **Modificar** en la columna "Permitir".
7. Haz clic en **Aplicar** y **Aceptar**.

## Notas sobre SSL y Logs
- **Logs**: Para mantener el disco limpio, el registro se realiza exclusivamente bajo demanda (es decir, cuando existen fallos de validación, excepciones técnicas o rechazos por parte de AFIP/ARCA). Las peticiones exitosas no dejan rastro persistente en disco.
- **SSL**: Las llamadas desde SAP hacia el backend se realizan nativamente. Según la configuración de la infraestructura, se utilizan conexiones locales (usualmente puerto 888) pudiendo bypassar las validaciones SSL en caso de certificados auto-firmados.
