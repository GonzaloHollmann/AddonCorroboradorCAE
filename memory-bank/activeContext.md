# Contexto Activo - Migración WebApiCAE

## Estado Actual
El sistema Backend (`WebApiCAE`) y Frontend (`CorroboradorCAE`) han sido reforzados con una robusta capa de validación de reglas de negocio en dos niveles. Se ha priorizado la integridad de los datos antes de consumir los servicios de AFIP. Actualmente, el sistema permite realizar pruebas técnicas end-to-end mediante una solución de anulación manual de CAE.

## Decisiones Técnicas Clave
- **Validación en Dos Capas**: 
  - **Frontend**: Chequeo exhaustivo en SAP antes de la llamada a la API (longitud de CUITs, formato de fecha, integridad de CAE e importes).
  - **Backend**: Re-validación de integridad del objeto `CAERequest` para protección de la API y retorno de errores `400 Bad Request`.
- **Backend Inteligente**: Centraliza todas las decisiones. Si el entorno es `HOMO`, responde inmediatamente con éxito simulado. Realiza el mapeo de letras de SAP ('A', 'B', etc.) a códigos de AFIP.
- **Lógica de CUIT Representante**: El backend utiliza el `CuitRepresentante` configurado en `appsettings.json` para todas las gestiones ante AFIP, actuando en nombre del dueño del certificado.
- **Llamado API Directo**: Se utiliza `HttpClient` directamente en el método `ValidarCaeAFIPAsync` con serialización `Newtonsoft.Json` nativa para asegurar un comportamiento predecible y evitar configuraciones complejas de proxy o SSL del entorno SAP.
- **DTOs Unificados**: Campos del DTO `CAERequest` alineados con la arquitectura del Portal de Proveedores.
- **Compatibilidad VS 2019**: Utiliza **.NET 5.0** con estructura estándar.
- **Estrategia de Prueba Técnica**: Se habilitó una solución temporal en el Frontend que permite ingresar manualmente el CAE en el campo `Comments` de la factura. Esto permite validar el circuito completo (AddOn -> Backend -> AFIP) sin depender de la lógica final de obtención del CAE en SAP.

## Reglas de Negocio Implementadas
1. **CAE**: Obligatorio, mínimo 14 dígitos.
2. **CUIT Emisor/Receptor**: Obligatorio, exactamente 11 dígitos numéricos.
3. **Punto de Venta**: Obligatorio, numérico, autocompletado a 5 dígitos.
4. **Fecha**: Obligatoria, formato YYYYMMDD.
5. **Importe**: Obligatorio, mayor a cero.
6. **Tipo Comprobante**: Letras válidas (A, B, C, M).

## Próximos Pasos
- Realizar prueba end-to-end contra homologación real desactivando el modo simulación en el Backend.
