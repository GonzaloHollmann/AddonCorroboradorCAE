using System;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Xml.Linq;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace WebApiCAE.Services
{
    public class ConstatadorCAEAFIP
    {
        private readonly IConfiguration _configuration;

        public ConstatadorCAEAFIP(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<(string resultadoConstatacion, string msg, string observaciones)> ConstatarCAEAsync(
                                 string token, string sign, string cuit,
                                 string cbteModo, long cuitEmisor,
                                 int ptoVta, int cbteTipo,
                                 long cbteNro, string cbteFch,
                                 double impTotal, string codAutorizacion,
                                 string docTipoReceptor, string docNroReceptor)
        {
            string EntornoAFIP = _configuration["AFIP:Entorno"] ?? "HOMO";
            
            string? resultadoConstatacion = null;
            string? msg = null;
            string? observaciones = null;

            string url = EntornoAFIP == "PROD" 
                ? "https://servicios1.afip.gob.ar/WSCDC/service.asmx" 
                : "https://wswhomo.afip.gov.ar/WSCDC/service.asmx";

            // Asegurar que el decimal se envía con punto y no coma, que es lo esperado por AFIP
            string impTotalStr = impTotal.ToString(System.Globalization.CultureInfo.InvariantCulture);

            string soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" 
               xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
  <soap:Body>
    <ComprobanteConstatar xmlns=""http://servicios1.afip.gob.ar/wscdc/"">
      <Auth>
        <Token>{token}</Token>
        <Sign>{sign}</Sign>
        <Cuit>{cuit}</Cuit>
      </Auth>
      <CmpReq>
        <CbteModo>{cbteModo}</CbteModo>
        <CuitEmisor>{cuitEmisor}</CuitEmisor>
        <PtoVta>{ptoVta}</PtoVta>
        <CbteTipo>{cbteTipo}</CbteTipo>
        <CbteNro>{cbteNro}</CbteNro>
        <CbteFch>{cbteFch}</CbteFch>
        <ImpTotal>{impTotalStr}</ImpTotal>
        <CodAutorizacion>{codAutorizacion}</CodAutorizacion>
        <DocTipoReceptor>{docTipoReceptor}</DocTipoReceptor>
        <DocNroReceptor>{docNroReceptor}</DocNroReceptor>
      </CmpReq>
    </ComprobanteConstatar>
  </soap:Body>
</soap:Envelope>";

            try
            {
                using var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                };

                using var httpClient = new HttpClient(handler);
                
                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPAction", "http://servicios1.afip.gob.ar/wscdc/ComprobanteConstatar");

                var response = await httpClient.PostAsync(url, content);
                string soapResult = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"HTTP Error {response.StatusCode}: {soapResult}");
                }

                XDocument doc = XDocument.Parse(soapResult);
                XNamespace ns = "http://servicios1.afip.gob.ar/wscdc/";

                var resultNode = doc.Descendants(ns + "ComprobanteConstatarResult").FirstOrDefault();
                if (resultNode == null)
                {
                    throw new Exception("No se encontró ComprobanteConstatarResult en la respuesta XML.");
                }

                resultadoConstatacion = resultNode.Element(ns + "Resultado")?.Value;
                var fchProceso = resultNode.Element(ns + "FchProceso")?.Value;

                var sbObservaciones = new StringBuilder();
                sbObservaciones.AppendLine($"Resultado: {resultadoConstatacion}");
                sbObservaciones.AppendLine($"FchProceso: {fchProceso}");

                var observacionesNode = resultNode.Element(ns + "Observaciones");
                if (observacionesNode != null && observacionesNode.HasElements)
                {
                    sbObservaciones.AppendLine("Observaciones:");
                    foreach (var obs in observacionesNode.Elements(ns + "Obs"))
                    {
                        var code = obs.Element(ns + "Code")?.Value;
                        var desc = obs.Element(ns + "Msg")?.Value;
                        sbObservaciones.AppendLine($"   Código: {code}, Descripción: {desc}");
                        msg = desc;
                    }
                }

                var errorsNode = resultNode.Element(ns + "Errors");
                if (errorsNode != null && errorsNode.HasElements)
                {
                    sbObservaciones.AppendLine("Errores:");
                    foreach (var err in errorsNode.Elements(ns + "Err"))
                    {
                        var code = err.Element(ns + "Code")?.Value;
                        var desc = err.Element(ns + "Msg")?.Value;
                        sbObservaciones.AppendLine($"   Código: {code}, Descripción: {desc}");
                        msg = desc;
                    }
                }

                var eventsNode = resultNode.Element(ns + "Events");
                if (eventsNode != null && eventsNode.HasElements)
                {
                    sbObservaciones.AppendLine("Eventos:");
                    foreach (var evt in eventsNode.Elements(ns + "Evt"))
                    {
                        var code = evt.Element(ns + "Code")?.Value;
                        var desc = evt.Element(ns + "Msg")?.Value;
                        sbObservaciones.AppendLine($"   Código: {code}, Descripción: {desc}");
                    }
                }

                observaciones = sbObservaciones.ToString();

                return (resultadoConstatacion ?? "", msg ?? "", observaciones ?? "");
            }
            catch (Exception ex)
            {
                msg = $"Error al constatar CAE: {ex.Message}";
                return (resultadoConstatacion ?? "", msg, observaciones ?? "");
            }
        }
    }
}
