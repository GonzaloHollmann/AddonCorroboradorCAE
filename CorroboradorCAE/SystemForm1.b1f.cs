using SAPbouiCOM.Framework;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;

namespace SBOAddonProject1
{
    [FormAttribute("141", "SystemForm1.b1f")]
    class SystemForm1 : SystemFormBase
    {
        public SystemForm1() { }
        public override void OnInitializeComponent() { this.OnCustomInitialize(); }
        public override void OnInitializeFormEvents() { this.DataAddBefore += new DataAddBeforeHandler(this.Form_DataAddBefore); }

        private void OnCustomInitialize()
        {
            SAPbouiCOM.Form oForm = (SAPbouiCOM.Form)this.UIAPIRawForm;
            SAPbouiCOM.Item oItem = oForm.Items.Add("btnVCAE", SAPbouiCOM.BoFormItemTypes.it_BUTTON);
            SAPbouiCOM.Item oItemRef = oForm.Items.Item("2");
            oItem.Top = oItemRef.Top;
            oItem.Left = oItemRef.Left + oItemRef.Width + 5;
            oItem.Width = 90;
            oItem.Height = oItemRef.Height;
            ((SAPbouiCOM.Button)oItem.Specific).Caption = "Validar CAE";
            ((SAPbouiCOM.Button)oItem.Specific).PressedAfter += (s, e) => ValidarCaeAFIPAsync();
        }

        private async Task ValidarCaeAFIPAsync()
        {
            try
            {
                string fCAE = "U_CAE";
                string fPV =  "PTICode";
                string fCuit = "LicTradNum";

                SAPbouiCOM.Form oForm = (SAPbouiCOM.Form)this.UIAPIRawForm;
                SAPbouiCOM.DBDataSource ds = oForm.DataSources.DBDataSources.Item("OPCH");

                string cuitE = Regex.Replace(ds.GetValue(fCuit, 0), @"[^\d]", "");
                string pVtaRaw = ds.GetValue(fPV, 0).Trim();
                string pVta = Regex.Replace(pVtaRaw, @"[^\d]", "").PadLeft(5, '0');
                string letra = ds.GetValue("Letter", 0).Trim().ToUpper();
                string cbteNroRaw = ds.GetValue("FolNumFrom", 0).Trim();
                string cbteNroStr = Regex.Replace(cbteNroRaw, @"[^\d]", "");
                string cbteFch = ds.GetValue("DocDate", 0).Trim(); 
                string codAut = ds.GetValue(fCAE, 0).Trim();

                double docTotalFC = ParseSAPAmount(ds.GetValue("DocTotalFC", 0));
                double docTotalLocal = ParseSAPAmount(ds.GetValue("DocTotal", 0));
                
                // Si el total en moneda extranjera es mayor a cero, usamos ese importe para AFIP
                double impTotal = (docTotalFC > 0) ? docTotalFC : docTotalLocal;
                
                string miCuit = Regex.Replace(GetMiCuit(), @"[^\d]", "");

                List<string> errors = new List<string>();
                if (string.IsNullOrEmpty(codAut)) errors.Add("- CAE (Código CAE) es obligatorio.");
                else if (codAut.Length < 14) errors.Add("- CAE debe tener al menos 14 dígitos.");

                if (string.IsNullOrEmpty(cuitE)) errors.Add("- CUIT del Emisor es obligatorio.");
                else if (cuitE.Length != 11) errors.Add("- CUIT del Emisor debe tener 11 dígitos.");

                if (string.IsNullOrEmpty(miCuit)) errors.Add("- CUIT del Receptor (Propio) no detectado en SAP.");
                else if (miCuit.Length != 11) errors.Add("- CUIT del Receptor debe tener 11 dígitos.");

                if (string.IsNullOrEmpty(pVtaRaw)) errors.Add("- Punto de Venta es obligatorio.");
                if (string.IsNullOrEmpty(cbteNroRaw)) errors.Add("- Número de comprobante es obligatorio.");
                if (string.IsNullOrEmpty(cbteFch)) errors.Add("- Fecha de comprobante es obligatoria.");
                else if (cbteFch.Length != 8) errors.Add("- Formato de fecha inválido (debe ser YYYYMMDD).");

                if (impTotal <= 0) errors.Add("- El importe total debe ser mayor a cero.");
                if (!new[] { "A", "B", "C", "M" }.Contains(letra)) errors.Add("- Letra de comprobante inválida o no soportada ('" + letra + "').");

                if (errors.Count > 0)
                {
                    Application.SBO_Application.MessageBox("No se puede validar el CAE debido a los siguientes errores:\n" + string.Join("\n", errors));
                    return;
                }

                Application.SBO_Application.StatusBar.SetText("Iniciando validación de CAE...", SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Warning);

                var requestData = new {
                    cbteModo = "CAE",
                    cuitEmisor = long.Parse(cuitE),
                    ptoVta = pVta,
                    cbteTipo = letra,
                    cbteNro = long.Parse(cbteNroStr),
                    cbteFch = cbteFch,
                    codAut = codAut,
                    docTipoRecep = "80",
                    docNroRecep = miCuit,
                    impTotal = impTotal
                };

                string jsonContent = Newtonsoft.Json.JsonConvert.SerializeObject(requestData);

                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost:888/api/CAE/constatar");
                    var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                    request.Content = content;
                    
                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    
                    string responseString = await response.Content.ReadAsStringAsync();

                    var res = Newtonsoft.Json.JsonConvert.DeserializeObject<CAEResponse>(responseString);

                    if (res != null && res.ResultadoConstatacion == "A") {
                        Application.SBO_Application.StatusBar.SetText("✅ CAE VÁLIDO.", SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Success);
                        string finalMsg = string.IsNullOrWhiteSpace(res.Msg) ? "✅ CAE validado correctamente" : "✅ CAE VALIDADO: " + res.Msg;
                        Application.SBO_Application.MessageBox(finalMsg);
                    } else {
                        string msgError = res != null ? res.Msg : "Respuesta nula de la API";
                        Logger.Error($"CAE RECHAZADO. Petición: {jsonContent} | Error: {msgError}");
                        Application.SBO_Application.StatusBar.SetText("❌ CAE RECHAZADO.", SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Error);
                        Application.SBO_Application.MessageBox("❌ RECHAZADO: " + msgError);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error en ValidarCaeAFIPAsync", ex);
                Application.SBO_Application.StatusBar.SetText("Error de validación: " + ex.Message, SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Error);
            }
        }

        private double ParseSAPAmount(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return 0;
            
            string cleanVal = Regex.Replace(rawValue, @"[^\d,\.-]", "").Trim();
            if (string.IsNullOrEmpty(cleanVal)) return 0;

            int lastComma = cleanVal.LastIndexOf(',');
            int lastDot = cleanVal.LastIndexOf('.');

            if (lastComma > lastDot)
            {
                cleanVal = cleanVal.Replace(".", "").Replace(",", ".");
            }
            else if (lastDot > lastComma)
            {
                cleanVal = cleanVal.Replace(",", "");
            }

            double result = 0;
            double.TryParse(cleanVal, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
            return result;
        }

        private string GetMiCuit()
        {
            SAPbobsCOM.Company comp = (SAPbobsCOM.Company)Application.SBO_Application.Company.GetDICompany();
            SAPbobsCOM.Recordset rs = (SAPbobsCOM.Recordset)comp.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            rs.DoQuery("SELECT \"TaxIdNum\" FROM OADM");
            return rs.EoF ? "" : Convert.ToString(rs.Fields.Item(0).Value);
        }

        private void Form_DataAddBefore(ref SAPbouiCOM.BusinessObjectInfo pVal, out bool BubbleEvent) { BubbleEvent = true; }
    }
}
