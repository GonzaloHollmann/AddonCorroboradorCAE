using Newtonsoft.Json;

namespace WebApiCAE.DTOs
{
    public class CAERequest
    {
        [JsonProperty("cbteModo")]
        public string cbteModo { get; set; } = string.Empty;

        [JsonProperty("cuitEmisor")]
        public long cuitEmisor { get; set; }

        [JsonProperty("ptoVta")]
        public string ptoVta { get; set; } = string.Empty;

        [JsonProperty("cbteTipo")]
        public string cbteTipo { get; set; } = string.Empty;

        [JsonProperty("cbteNro")]
        public long cbteNro { get; set; }

        [JsonProperty("cbteFch")]
        public string cbteFch { get; set; } = string.Empty;

        [JsonProperty("codAut")]
        public string codAut { get; set; } = string.Empty;

        [JsonProperty("docNroRecep")]
        public string docNroRecep { get; set; } = string.Empty;

        [JsonProperty("docTipoRecep")]
        public string docTipoRecep { get; set; } = string.Empty;

        [JsonProperty("impTotal")]
        public double impTotal { get; set; }
    }

    public class CAEResponse
    {
        public string ResultadoConstatacion { get; set; } = string.Empty;
        public string Msg { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
    }
}
