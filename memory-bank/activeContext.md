# Contexto Activo - Migración WebApiCAE y Soporte Multicomprobante

## Estado Actual
El sistema Backend (`WebApiCAE`) y Frontend (`CorroboradorCAE`) han sido adaptados para dar soporte integral a los 4 tipos de formularios de compras de SAP Business One: Facturas de Proveedores (`141`), Notas de Crédito de Proveedores (`181`), Notas de Débito de Proveedores (`65306`) y Facturas de Reserva de Proveedores (`60092`). La arquitectura fue reforzada con manejo global de excepciones en el Backend (retornando siempre HTTP 200 con `CAEResponse` para evitar errores 500 no descriptivos en SAP), sanitización estricta en el mapeo AFIP y compatibilidad dual de serialización (`Newtonsoft.Json` + `System.Text.Json`).

## Decisiones Técnicas Clave
- **Manejo Global de Excepciones y Diagnóstico Amigable**:
  - En `CAEController.cs`, todo el flujo del endpoint `constatar` está cubierto por un bloque global `try-catch`.
  - Ante cualquier excepción o error de validación interna, **no** se devuelve un código HTTP 500 ni 400. Se retorna `Ok()` (HTTP 200) con el objeto `CAEResponse` indicando `ResultadoConstatacion = "E"` y el detalle exacto de la excepción (`ex.Message`, `InnerException` y `StackTrace`).
  - Esto garantiza que el Frontend no interrumpa su flujo por `HttpRequestException` y pueda mostrar un `MessageBox` con la información precisa del error para diagnóstico inmediato.
- **Robustez y Sanitización en el Mapeo AFIP (`MapearTipoAFIP`)**:
  - Se aplica `.Trim().ToUpper()` sobre la letra del comprobante y el tipo de documento.
  - La matriz AFIP cubre:
    - **FACTURA**: A = 1, B = 6, C = 11, M = 51
    - **ND (Nota de Débito)**: A = 2, B = 7, C = 12, M = 52
    - **NC (Nota de Crédito)**: A = 3, B = 8, C = 13, M = 53
  - Si se recibe una combinación no válida o vacía, lanza una excepción explícita que es capturada y reportada inmediatamente en el mensaje de error.
- **Compatibilidad Dual de Serialización en DTOs**:
  - `CAEDTOs.cs` cuenta con atributos tanto de `Newtonsoft.Json` (`[JsonProperty]`) como de `System.Text.Json` (`[JsonPropertyName]`) para asegurar la vinculación correcta de propiedades en cualquier contexto de deserialización.
- **Soporte de los 4 Formularios SAP B1**:
  - **Formulario 141**: Facturas de Proveedores estándar (tabla `OPCH`).
  - **Formulario 181**: Notas de Crédito de Proveedores (tabla `ORPC`).
  - **Formulario 65306**: Notas de Débito de Proveedores (tabla `OPCH`).
  - **Formulario 60092**: Facturas de Reserva de Proveedores (tabla `OPCH`).
- **Refactorización Limpia con `CaeValidationHelper`**:
  - Inyección dual de botón (`SystemFormBase` + `SBO_Application_ItemEvent`).
  - Control de idempotencia y antirrebote (`_isValidating`).
  - Detección automática y escalable de documento y tabla (`DeterminarDocumento`).
- **Exclusión de Artefactos de Publicación**:
  - Se agregó la regla `PUB/` al `.gitignore` y se desvincularon del índice de Git los zips existentes (`PUB/1.0.1.zip` a `1.0.10.zip`) para evitar subir paquetes binarios a GitHub, preservando los archivos locales.

## Reglas de Negocio Implementadas
1. **CAE**: Obligatorio, mínimo 14 dígitos.
2. **CUIT Emisor/Receptor**: Obligatorio, exactamente 11 dígitos numéricos.
3. **Punto de Venta**: Obligatorio, numérico, autocompletado a 5 dígitos.
4. **Fecha**: Obligatoria, formato YYYYMMDD.
5. **Importe**: Obligatorio, mayor a cero.
6. **Tipo Comprobante**: Letras válidas (A, B, C, M) cruzadas con el TipoDocumento ('FACTURA', 'ND', 'NC').

## Próximos Pasos
- Probar la validación de Factura normal (141) y verificar en SAP el mensaje descriptivo si ocurriese algún fallo de conexión con AFIP o certificados.
