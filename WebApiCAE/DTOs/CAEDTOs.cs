using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace WebApiCAE.DTOs
{
    public class CAERequest
    {
        [JsonProperty("cbteModo")]
        [JsonPropertyName("cbteModo")]
        public string cbteModo { get; set; } = string.Empty;

        [JsonProperty("cuitEmisor")]
        [JsonPropertyName("cuitEmisor")]
        public long cuitEmisor { get; set; }

        [JsonProperty("ptoVta")]
        [JsonPropertyName("ptoVta")]
        public string ptoVta { get; set; } = string.Empty;

        [JsonProperty("cbteTipo")]
        [JsonPropertyName("cbteTipo")]
        public string cbteTipo { get; set; } = string.Empty;

        [JsonProperty("cbteNro")]
        [JsonPropertyName("cbteNro")]
        public long cbteNro { get; set; }

        [JsonProperty("cbteFch")]
        [JsonPropertyName("cbteFch")]
        public string cbteFch { get; set; } = string.Empty;

        [JsonProperty("codAut")]
        [JsonPropertyName("codAut")]
        public string codAut { get; set; } = string.Empty;

        [JsonProperty("docNroRecep")]
        [JsonPropertyName("docNroRecep")]
        public string docNroRecep { get; set; } = string.Empty;

        [JsonProperty("docTipoRecep")]
        [JsonPropertyName("docTipoRecep")]
        public string docTipoRecep { get; set; } = string.Empty;

        [JsonProperty("tipoDocumento")]
        [JsonPropertyName("tipoDocumento")]
        public string TipoDocumento { get; set; } = "FACTURA";

        [JsonProperty("impTotal")]
        [JsonPropertyName("impTotal")]
        public double impTotal { get; set; }
    }

    public class CAEResponse
    {
        [JsonProperty("ResultadoConstatacion")]
        [JsonPropertyName("ResultadoConstatacion")]
        public string ResultadoConstatacion { get; set; } = string.Empty;

        [JsonProperty("Msg")]
        [JsonPropertyName("Msg")]
        public string Msg { get; set; } = string.Empty;

        [JsonProperty("Observaciones")]
        [JsonPropertyName("Observaciones")]
        public string Observaciones { get; set; } = string.Empty;
    }
}
