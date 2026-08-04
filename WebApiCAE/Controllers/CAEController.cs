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
                        string jsonRequest = System.Text.Json.JsonSerializer.Serialize(request);
                        System.IO.File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - BAD REQUEST: {jsonRequest} | ERROR: {validationErrors}{Environment.NewLine}");
                    }
                    catch { }
                    return BadRequest(new CAEResponse { 
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
                int cbteTipoCodigo = MapearTipoAFIP(request.cbteTipo);

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
                    System.IO.File.AppendAllText(@"C:\inetpub\wwwroot\CorroboradorCAE\log_api.txt", $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - ERROR: {ex.ToString()}{Environment.NewLine}");
                }
                catch { }

                _logger.LogError(ex, "Error al procesar la constatación de CAE.");
                return StatusCode(500, new CAEResponse
                {
                    Msg = $"Error interno: {ex.Message}"
                });
            }
        }

        private int MapearTipoAFIP(string letra)
        {
            if (string.IsNullOrEmpty(letra)) return 1;
            switch (letra.ToUpper())
            {
                case "A": return 1;   // Portal '001'
                case "B": return 6;   // Portal '006'
                case "C": return 11;  // Portal '011'
                case "M": return 51;  // Portal '051'
                default: return 1;
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
