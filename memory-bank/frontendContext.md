# Contexto del Frontend - Refactorización CorroboradorCAE

## Estado Actual
El proyecto frontend (`CorroboradorCAE`) ha sido actualizado para actuar como el primer filtro de validación de datos y permitir pruebas técnicas flexibles mediante el uso del campo `Comments` de SAP para la entrada manual de CAE.

## Cambios Clave
- **Validación Local (Pre-vuelo)**: Antes de disparar la petición asíncrona, el Add-on verifica:
  - Presencia y longitud de CUITs (11 dígitos).
  - Formato de fecha (YYYYMMDD).
  - Integridad de CAE (mínimo 14 dígitos).
  - Validez de la letra del comprobante.
- **Anulación Manual de CAE para Pruebas**: Se implementó una lógica temporal que lee el campo `Comments` de la factura. Si tiene contenido, se utiliza como el CAE del request, permitiendo probar el circuito técnico con valores de homologación sin depender de la integración funcional definitiva.
- **Feedback Directo**: Si faltan datos o son erróneos, se muestra un `MessageBox` detallado con la lista de errores encontrados.
- **Simplificación de UI**: `SystemForm1.b1f.cs` mantiene su estructura ligera, centralizando las reglas de interfaz en el método `ValidarCaeAFIP`.

## Detalles Técnicos
- **Framework**: .NET Framework 4.8.1.
- **Validación**: Basada en colecciones de errores para reportar múltiples fallos en una sola alerta visual.
- **Comunicación**: HTTP POST con contenido JSON vía `CaeApiClient.cs`.
