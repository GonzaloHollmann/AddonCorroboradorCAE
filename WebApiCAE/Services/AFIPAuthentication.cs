using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Pkcs;
using System.IO;
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace WebApiCAE.Services
{
    public class AFIPAuthentication
    {
        private readonly IConfiguration _configuration;

        public AFIPAuthentication(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        #region CORROBORADOR
        public async Task<(string token, string sign)> ObtenerTokenConstatador()
        {
            var ticketPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ResourcesAFIP", "ticket_Constatador.xml");

            try
            {
                string token, sign;

                if (File.Exists(ticketPath))
                {
                    var ticketXml = XDocument.Load(ticketPath);
                    var expirationTimeStr = ticketXml.Descendants("expirationTime").First().Value;
                    var expirationTime = DateTime.Parse(expirationTimeStr);

                    if (expirationTime > DateTime.Now)
                    {
                        token = ticketXml.Descendants("token").First().Value;
                        sign = ticketXml.Descendants("sign").First().Value;
                        return (token, sign);
                    }
                }

                string tra = GenerarTRAConstatador();
                string EntornoAFIP = _configuration["AFIP:Entorno"];
                string PasswordCertAFIP = _configuration["AFIP:CertPassword"];
                string CertName = _configuration["AFIP:CertName"];
                string certPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ResourcesAFIP", CertName);

                if (!File.Exists(certPath))
                {
                    throw new FileNotFoundException($"No se encontró el certificado en la ruta: {certPath}");
                }

                X509Certificate2 cert = new X509Certificate2(
                    certPath,
                    PasswordCertAFIP,
                    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                string traFirmado = FirmarTRA(tra, cert);

                string ticketRespuesta;
                if (EntornoAFIP == "PROD")
                {
                    ticketRespuesta = await EnviarTRA(traFirmado, "https://wsaa.afip.gov.ar/ws/services/LoginCms");
                }
                else
                {
                    ticketRespuesta = await EnviarTRA(traFirmado, "https://wsaahomo.afip.gov.ar/ws/services/LoginCms");
                }

                (token, sign) = ExtraerTokenYSign(ticketRespuesta);

                var newTicketXml = new XDocument(
                    new XElement("ticket",
                        new XElement("token", token),
                        new XElement("sign", sign),
                        new XElement("expirationTime", DateTime.Now.AddMinutes(10).ToString("yyyy-MM-ddTHH:mm:ss"))
                    )
                );

                Directory.CreateDirectory(Path.GetDirectoryName(ticketPath));
                newTicketXml.Save(ticketPath);

                return (token, sign);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al obtener Token y Sign para el constatador. Detalles: {ex.Message}", ex);
            }
        }

        private string GenerarTRAConstatador()
        {
            var tra = new StringBuilder();
            tra.AppendLine("<loginTicketRequest version=\"1.0\">");
            tra.AppendLine("<header>");
            tra.AppendLine($"<uniqueId>{DateTimeOffset.Now.ToUnixTimeSeconds()}</uniqueId>");
            tra.AppendLine($"<generationTime>{DateTime.Now.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ss")}</generationTime>");
            tra.AppendLine($"<expirationTime>{DateTime.Now.AddMinutes(10).ToString("yyyy-MM-ddTHH:mm:ss")}</expirationTime>");
            tra.AppendLine("</header>");
            tra.AppendLine("<service>wscdc</service>");
            tra.AppendLine("</loginTicketRequest>");
            return tra.ToString();
        }
        #endregion

        private string FirmarTRA(string tra, X509Certificate2 cert)
        {
            try
            {
                byte[] traBytes = Encoding.UTF8.GetBytes(tra);
                ContentInfo content = new ContentInfo(traBytes);
                SignedCms signedCms = new SignedCms(content);
                CmsSigner signer = new CmsSigner(cert);

                signer.DigestAlgorithm = new Oid("1.3.14.3.2.26"); // SHA1

                signer.IncludeOption = X509IncludeOption.EndCertOnly;

                signedCms.ComputeSignature(signer);
                byte[] signedBytes = signedCms.Encode();
                return Convert.ToBase64String(signedBytes);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al firmar el TRA: {ex.Message}", ex);
            }
        }

        private async Task<string> EnviarTRA(string traFirmado, string wsaaUrl)
        {
            try
            {
                string soapEnvelope = $@"
                    <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ser=""http://wsaa.view.sua.dvadac.desein.afip.gov"">
                        <soapenv:Header/>
                        <soapenv:Body>
                            <ser:loginCms>
                                <arg0>{traFirmado}</arg0>
                            </ser:loginCms>
                        </soapenv:Body>
                    </soapenv:Envelope>";

                using var httpClient = new HttpClient();
                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPAction", "");

                var response = await httpClient.PostAsync(wsaaUrl, content);
                string soapResult = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error AFIP: {soapResult}");
                }

                return soapResult;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al enviar el TRA: {ex.Message}", ex);
            }
        }

        private (string token, string sign) ExtraerTokenYSign(string ticketRespuesta)
        {
            try
            {
                XDocument doc = XDocument.Parse(ticketRespuesta);
                string loginCmsReturn = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "loginCmsReturn")?.Value;

                if (string.IsNullOrEmpty(loginCmsReturn))
                    throw new Exception("No se encontró loginCmsReturn en la respuesta.");

                XDocument loginTicketResponse = XDocument.Parse(loginCmsReturn);

                string token = loginTicketResponse.Descendants("token").FirstOrDefault()?.Value;
                string sign = loginTicketResponse.Descendants("sign").FirstOrDefault()?.Value;

                if (token != null) token = token.Trim().Replace("\n", "").Replace("\r", "");
                if (sign != null) sign = sign.Trim().Replace("\n", "").Replace("\r", "");

                return (token, sign);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al procesar el XML de respuesta de AFIP.", ex);
            }
        }
    }
}
