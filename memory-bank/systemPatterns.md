# Patrones del Sistema - AddonCorroboradorCAE

## Arquitectura General
El sistema opera bajo un modelo desacoplado de dos niveles:
1. **Frontend (SAP Business One Add-on - .NET Framework 4.8)**: Actúa como capa de interacción y captura de datos desde los formularios de SAP Business One.
2. **Backend (ASP.NET Core Web API - .NET 5.0)**: Centraliza la lógica de negocio, autenticación criptográfica contra AFIP (WSAA / WSCDC), traducción de comprobantes y persistencia de auditoría/logs.

```
+-----------------------------------+       HTTP POST (JSON)       +-------------------------+       SOAP / HTTPS       +------------------+
|      SAP Business One Client      |  ------------------------>  |   WebApiCAE (Backend)   |  ---------------------->  |   AFIP WSCDC     |
| (Forms 141, 181, 65306, 60092)    |                             | (Kestrel / IIS Local)   |                           | (Constatador CAE)|
+-----------------------------------+                             +-------------------------+                           +------------------+
```

## Patrones de Frontend (Add-on SAP)
- **Centralización en `CaeValidationHelper`**:
  - Toda la lógica de interacción con SAP B1, cálculo de importes, validaciones, resolución de documentos y comunicación HTTP con la WebAPI está unificada en `CaeValidationHelper.cs`.
- **Soporte Multiformulario Dual (Declarativo + Eventos Globales)**:
  - **Mecanismo Declarativo**: En `SystemForm1.b1f.cs` se definen clases derivadas de `SystemFormBase` decoradas con `[FormAttribute]` para los 4 formularios soportados:
    - `141`: Factura de Proveedores
    - `181`: Nota de Crédito de Proveedores
    - `65306`: Nota de Débito de Proveedores
    - `60092`: Factura de Reserva de Proveedores
  - **Mecanismo Global (`ItemEvent`)**: En `Program.cs`, se conecta `CaeValidationHelper.SBO_Application_ItemEvent` que escucha `et_FORM_LOAD`, `et_FORM_VISIBLE` y `et_FORM_RESIZE` sobre los 4 FormTypes.
  - **Idempotencia y Antirrebote**:
    - `AddValidationButton` revisa si `btnVCAE` ya existe en el formulario antes de agregarlo, impidiendo duplicaciones sin importar qué mecanismo se dispare primero.
    - `_isValidating` impide llamadas concurrentes dobles ante doble clic rápido.
- **Resolución Dinámica de DataSources y TipoDocumento (`DeterminarDocumento`)**:
  - **Form 181**: Tabla `ORPC` -> `TipoDocumento = "NC"`.
  - **Form 65306**: Tabla `OPCH` -> `TipoDocumento = "ND"`.
  - **Form 60092**: Tabla `OPCH` -> `TipoDocumento = "FACTURA"`.
  - **Form 141**: Tabla `OPCH` -> Inspecciona `DocSubType`: si es `"DN"` -> `"ND"`, caso contrario -> `"FACTURA"`.
  - **Inspección Defensiva de Tablas**: Si el formulario no coincide de forma exacta con los IDs, revisa la colección `DBDataSources` buscando `ORPC` u `OPCH`.
- **Validación Preventiva Local (Fail Fast)**:
  - Verificación previa al llamado de red (longitud de CUITs, formato de fecha YYYYMMDD, importe > 0, CAE >= 14 dígitos). Si falla, alerta visual acumulativa y aborta antes de consumir recursos.
- **Comunicación HTTP Nativa**:
  - `HttpClient` directo con `Newtonsoft.Json` para garantizar aislamiento y compatibilidad en el runtime de SAP.

## Patrones de Backend (WebApiCAE)
- **Mapeo de Comprobantes AFIP**:
  - Matriz desacoplada en `CAEController.MapearTipoAFIP(letra, tipoDocumento)`:
    - **FACTURA**: A = 1, B = 6, C = 11, M = 51
    - **ND (Nota de Débito)**: A = 2, B = 7, C = 12, M = 52
    - **NC (Nota de Crédito)**: A = 3, B = 8, C = 13, M = 53
- **DTOs Fuertes**:
  - `CAERequest`: Contiene `tipoDocumento`, `cbteTipo` (letra), `cbteNro`, `cuitEmisor`, `docNroRecep`, `impTotal`, etc.
- **Entorno Simulado vs Real**:
  - Configuración vía `appsettings.json` (`AFIP:Entorno`). En `HOMO` simula respuesta inmediata `A` para acelerar testing funcional. En `PROD` solicita ticket WSAA y consulta el WSCDC real de AFIP.
- **Manejo Seguro de Excepciones y Logging**:
  - Solo se loguean peticiones fallidas, excepciones o rechazos de AFIP para preservar espacio y performance.

