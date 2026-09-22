using Microsoft.AspNetCore.Mvc;
using WebApiCAE.DTOs;
using WebApiCAE.Services;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;

namespace WebApiCAE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CAEController : ControllerBase
    {
        private readonly AFIPAuthentication _afipAuth;
        private readonly ConstatadorCAEAFIP _constatador;

        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly Microsoft.Extensions.Logging.ILogger<CAEController> _logger;

        public CAEController(AFIPAuthentication afipAuth, ConstatadorCAEAFIP constatador, Microsoft.Extensions.Configuration.IConfiguration configuration, Microsoft.Extensions.Logging.ILogger<CAEController> logger)
        {
            _afipAuth = afipAuth;
            _constatador = constatador;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("constatar")]
        public async Task<ActionResult<CAEResponse>> Constatar([FromBody] CAERequest request)
        {
            string logPath = @"C:\inetpub\wwwroot\CorroboradorCAE\log_api.txt";
            try
            {
                // 0. Validaciones de Integridad
                string validationErrors = ValidarRequest(request);
                if (!string.IsNullOrEmpty(validationErrors))
                {
                    try
                    {
                        string dir = System.IO.Path.GetDirectoryName(logPath);
                        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                            System.IO.Directory.CreateDirectory(dir);

                        string jsonRequest = System.Text.Json.JsonSerializer.Serialize(request);
                        System.IO.File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - BAD REQUEST: {jsonRequest} | ERROR: {validationErrors}{Environment.NewLine}");
                    }
                    catch { }

                    return Ok(new CAEResponse { 
                        ResultadoConstatacion = "E", 
                        Msg = "Error de validación en el Backend: " + validationErrors 
                    });
                }

                string entorno = _configuration["AFIP:Entorno"] ?? "HOMO";
                string cuitRepresentante = _configuration["AFIP:CuitRepresentante"] ?? "20264737614";

                if (entorno == "HOMO")
                {
                    return Ok(new CAEResponse
                    {
                        ResultadoConstatacion = "A",
                        Msg = "[SIMULADO] El CAE es válido",
                        Observaciones = "Modo Homologación activado en el Backend."
                    });
                }

                // 1. Mapeo de Tipo de Comprobante a código AFIP (numérico)
                int cbteTipoCodigo = MapearTipoAFIP(request.cbteTipo, request.TipoDocumento);

                // 2. Obtener Token y Sign (PROD) usando CuitRepresentante
                var (token, sign) = await _afipAuth.ObtenerTokenConstatador();

                // CRÍTICO: Redondeo a 2 decimales para evitar colas de decimales infinitas en AFIP
                double impTotalRedondeado = Math.Round(request.impTotal, 2);

                // 3. Constatar CAE (PROD)
                var (resultado, msg, observaciones) = await _constatador.ConstatarCAEAsync(
                    token,
                    sign,
                    cuitRepresentante, // Quien consulta
                    request.cbteModo,
                    request.cuitEmisor,
                    int.Parse(request.ptoVta),
                    cbteTipoCodigo,
                    request.cbteNro,
                    request.cbteFch,
                    impTotalRedondeado,
                    request.codAut,
                    request.docTipoRecep,
                    request.docNroRecep // Receptor de la factura
                );

                if (resultado != "A")
                {
                    try
                    {
                        string jsonRequest = System.Text.Json.JsonSerializer.Serialize(request);
                        System.IO.File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - RECHAZADO: {jsonRequest} | RESPONSE: {resultado} - {msg} - {observaciones}{Environment.NewLine}");
                    }
                    catch { }
                }

                return Ok(new CAEResponse
                {
                    ResultadoConstatacion = resultado,
                    Msg = msg,
                    Observaciones = observaciones
                });
            }
            catch (Exception ex)
            {
                try
                {
                    string dir = System.IO.Path.GetDirectoryName(logPath);
                    if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                        System.IO.Directory.CreateDirectory(dir);

                    System.IO.File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - ERROR: {ex}{Environment.NewLine}");
                }
                catch { }

                string errorDetalle = ex.InnerException != null 
                    ? $"{ex.Message} --> {ex.InnerException.Message}" 
                    : ex.Message;

                _logger.LogError(ex, "Error al procesar la constatación de CAE: " + errorDetalle);

                return Ok(new CAEResponse
                {
                    ResultadoConstatacion = "E",
                    Msg = $"Error Interno API: {errorDetalle} | StackTrace: {ex.StackTrace}"
                });
            }
        }

        private int MapearTipoAFIP(string letra, string tipoDocumento)
        {
            string l = (letra ?? "").Trim().ToUpper();
            string tipo = (tipoDocumento ?? "FACTURA").Trim().ToUpper();

            switch (tipo)
            {
                case "FACTURA":
                    switch (l)
                    {
                        case "A": return 1;
                        case "B": return 6;
                        case "C": return 11;
                        case "M": return 51;
                        default:
                            throw new Exception($"Tipo de documento o letra no soportado: TipoDocumento='{tipoDocumento}', Letra='{letra}'");
                    }

                case "ND": // Nota de Débito
                    switch (l)
                    {
                        case "A": return 2;
                        case "B": return 7;
                        case "C": return 12;
                        case "M": return 52;
                        default:
                            throw new Exception($"Tipo de documento o letra no soportado: TipoDocumento='{tipoDocumento}', Letra='{letra}'");
                    }

                case "NC": // Nota de Crédito
                    switch (l)
                    {
                        case "A": return 3;
                        case "B": return 8;
                        case "C": return 13;
                        case "M": return 53;
                        default:
                            throw new Exception($"Tipo de documento o letra no soportado: TipoDocumento='{tipoDocumento}', Letra='{letra}'");
                    }

                default:
                    throw new Exception($"Tipo de documento o letra no soportado: TipoDocumento='{tipoDocumento}', Letra='{letra}'");
            }
        }

        private string ValidarRequest(CAERequest r)
        {
            if (r == null) return "Request nulo.";
            var errors = new System.Collections.Generic.List<string>();

            if (string.IsNullOrEmpty(r.codAut)) errors.Add("CAE ausente.");
            if (r.cuitEmisor <= 0) errors.Add("CUIT Emisor inválido.");
            if (string.IsNullOrEmpty(r.docNroRecep)) errors.Add("CUIT Receptor ausente.");
            if (string.IsNullOrEmpty(r.cbteFch)) errors.Add("Fecha ausente.");
            if (r.impTotal <= 0) errors.Add("Importe debe ser > 0.");
            if (string.IsNullOrEmpty(r.ptoVta)) errors.Add("Punto de venta ausente.");

            return string.Join(" | ", errors);
        }
    }
}
