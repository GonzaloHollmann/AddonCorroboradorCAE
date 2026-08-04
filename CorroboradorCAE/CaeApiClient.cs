using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Configuration;
using System.IO;

namespace SBOAddonProject1
{
    public class CaeApiClient
    {
        private static HttpClient _httpClient;
        private readonly string _baseUrl;

        public CaeApiClient()
        {
            // FORZAR TLS 1.2 ANTES DE TODO (Crítico para SAP)
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 |
                System.Net.SecurityProtocolType.Tls11 |
                System.Net.SecurityProtocolType.Tls;

            // BYPASS GLOBAL DE CERTIFICADOS (Más fuerte que el del handler)
            System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            if (_httpClient == null)
            {
                var handler = new HttpClientHandler
                {
                    // Algunos entornos requieren esto además del global
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                    UseProxy = false // Evita que SAP intente salir por un proxy para llegar a 127.0.0.1
                };
                _httpClient = new HttpClient(handler);
            }

            string configUrl = ConfigurationManager.AppSettings["WebApiUrl"];
            _baseUrl = !string.IsNullOrEmpty(configUrl) ? configUrl : "https://localhost:888";
            Logger.Info($"CaeApiClient inicializado. WebApiUrl en app.config: '{configUrl}', _baseUrl final: '{_baseUrl}'");
        }

        public async Task<CAEResponse> ValidarCaeAsync(CAERequest request)
        {
            try
            {
                var json = JsonConvert.SerializeObject(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = $"{_baseUrl.TrimEnd('/')}/api/CAE/constatar";

                Logger.Info($"Enviando petición a: {url}");

                var response = await _httpClient.PostAsync(url, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Error($"Error en la API. Status: {response.StatusCode}. Response: {responseJson}");
                    return new CAEResponse
                    {
                        ResultadoConstatacion = "Error",
                        Msg = $"Error en la API: {response.StatusCode} - {responseJson}"
                    };
                }

                Logger.Info("Petición exitosa recibida.");
                return JsonConvert.DeserializeObject<CAEResponse>(responseJson);
            }
            catch (Exception ex)
            {
                Logger.Error("EXCEPCIÓN al conectar con la API", ex);

                // 3. Extraemos el mensaje real (InnerException) para que lo puedas ver directamente en SAP
                string errorReal = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                if (ex.InnerException?.InnerException != null)
                {
                    errorReal += $" -> {ex.InnerException.InnerException.Message}";
                }

                return new CAEResponse
                {
                    ResultadoConstatacion = "Error",
                    Msg = $"Error conexión API: {errorReal}"
                };
            }
        }
    }

    public class CAERequest
    {
        [JsonProperty("cbteModo")]
        public string cbteModo { get; set; }

        [JsonProperty("cuitEmisor")]
        public long cuitEmisor { get; set; }

        [JsonProperty("ptoVta")]
        public string ptoVta { get; set; }

        [JsonProperty("cbteTipo")]
        public string cbteTipo { get; set; }

        [JsonProperty("cbteNro")]
        public long cbteNro { get; set; }

        [JsonProperty("cbteFch")]
        public string cbteFch { get; set; }

        [JsonProperty("codAut")]
        public string codAut { get; set; }

        [JsonProperty("docNroRecep")]
        public string docNroRecep { get; set; }

        [JsonProperty("docTipoRecep")]
        public string docTipoRecep { get; set; }

        [JsonProperty("impTotal")]
        public double impTotal { get; set; }
    }

    public class CAEResponse
    {
        public string ResultadoConstatacion { get; set; }
        public string Msg { get; set; }
        public string Observaciones { get; set; }
    }
}