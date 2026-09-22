# Progreso - Proyecto WebApiCAE

## Resumen del Estado
La infraestructura de validación y soporte multiformulario está completamente integrada y refactorizada. El Add-on cubre los 4 formularios nativos de compras de SAP Business One: Facturas de Proveedores (141), Facturas de Reserva (60092), Notas de Débito (65306) y Notas de Crédito (181), delegando la lógica a una clase `CaeValidationHelper` con soporte dual (declarativo e ItemEvent). El Backend cuenta con manejo global de excepciones con retorno HTTP 200 detallado, protección contra códigos 500 y mapeo AFIP estricto.

## Hitos Completados
- [x] Analizar la arquitectura del Portal de Proveedores.
- [x] Unificar `CAERequest` DTO en ambos proyectos.
- [x] Implementar lógica de `CuitRepresentante` en el Backend.
- [x] Implementar validaciones detalladas en el Frontend.
- [x] Implementar validaciones de integridad en el Backend.
- [x] Mover el mapeo de letras de comprobante al Backend.
- [x] Habilitar bypass manual de CAE vía `Comments` para pruebas técnicas.
- [x] Traducir y actualizar los Memory Banks con la nueva estrategia de pruebas.
- [x] Migrar el llamado API a `HttpClient` directo con serialización manual para asegurar el correcto funcionamiento dentro del entorno de SAP.
- [x] Agregar propiedad `TipoDocumento` a `CAERequest` en Backend y Frontend.
- [x] Implementar matriz completa de mapeo de comprobantes AFIP en Backend (Factura: 1, 6, 11, 51; ND: 2, 7, 12, 52; NC: 3, 8, 13, 53).
- [x] Escucha multiformulario en el Frontend para Forms 141 (OPCH) y 181 (ORPC).
- [x] Lógica de resolución dinámica de DBDataSource (`OPCH` vs `ORPC`) y de TipoDocumento (`DocSubType == "DN"` -> `ND`, `181` -> `NC`, default -> `FACTURA`).
- [x] Identificar FormTypes reales en SAP B1: 65306 (Nota de Débito) y 60092 (Factura de Reserva).
- [x] Refactorizar la arquitectura del Frontend a `CaeValidationHelper.cs` con métodos centralizados (`DeterminarDocumento`, `AddValidationButton`, `ValidarCaeAFIPAsync`).
- [x] Configurar clases `SystemFormBase` (`SystemFormFactura`, `SystemFormNC`, `SystemFormND`, `SystemFormReserva`) y manejador global `SBO_Application_ItemEvent`.
- [x] Envolver el endpoint `constatar` en `try-catch` global retornando HTTP 200 con `CAEResponse` descriptivo en caso de error interno o validación.
- [x] Agregar sanitización y validación estricta con lanzamiento de excepción en `MapearTipoAFIP`.
- [x] Agregar atributos de serialización dual (`System.Text.Json` + `Newtonsoft.Json`) en `CAEDTOs.cs`.
- [x] Ignorar carpeta `PUB/` en el repositorio Git y desindexar archivos de versiones (.zip) existentes sin eliminarlos localmente.

## Objetivo Actual
- [x] Validar técnicamente el circuito completo AddOn -> Backend -> AFIP con soporte para los 4 formularios (141, 181, 65306, 60092) y manejo transparente de errores.

## Tareas Pendientes
- Probar la validación de Factura normal (141) y corroborar el mensaje recibido en el Add-on.
- Realizar prueba end-to-end con homologación / producción en cada uno de los 4 formularios.
