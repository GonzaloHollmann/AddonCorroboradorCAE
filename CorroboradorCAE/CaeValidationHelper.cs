using SAPbouiCOM.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SBOAddonProject1
{
    public static class CaeValidationHelper
    {
        private static bool _isValidating = false;

        public static bool IsSupportedForm(string formTypeEx, int formTypeInt)
        {
            string ft = (formTypeEx ?? "").Replace("-", "").Trim();
            int fti = Math.Abs(formTypeInt);

            return ft == "141" || fti == 141 ||       // Factura de Proveedores
                   ft == "181" || fti == 181 ||       // Nota de Crédito de Proveedores
                   ft == "65306" || fti == 65306 ||   // Nota de Débito de Proveedores
                   ft == "60092" || fti == 60092;     // Factura de Reserva de Proveedores
        }

        public static void DeterminarDocumento(SAPbouiCOM.Form oForm, out string tableName, out string tipoDocumento)
        {
            string formType = (oForm.TypeEx ?? "").Replace("-", "").Trim();
            int formTypeInt = Math.Abs(oForm.Type);

            // 1. Detección directa por FormType de SAP B1
            if (formType == "181" || formTypeInt == 181)
            {
                tableName = "ORPC";
                tipoDocumento = "NC";
                return;
            }

            if (formType == "65306" || formTypeInt == 65306)
            {
                tableName = "OPCH";
                tipoDocumento = "ND";
                return;
            }

            if (formType == "60092" || formTypeInt == 60092)
            {
                tableName = "OPCH";
                tipoDocumento = "FACTURA";
                return;
            }

            if (formType == "141" || formTypeInt == 141)
            {
                tableName = "OPCH";
                tipoDocumento = "FACTURA";
                try
                {
                    var ds = oForm.DataSources.DBDataSources.Item("OPCH");
                    string docSubType = ds.GetValue("DocSubType", 0).Trim();
                    if (docSubType.Equals("DN", StringComparison.OrdinalIgnoreCase))
                    {
                        tipoDocumento = "ND";
                    }
                }
                catch { }
                return;
            }

            // 2. Fallback defensivo por análisis de DataSources presentes
            bool hasORPC = false;
            bool hasOPCH = false;
            try
            {
                for (int i = 0; i < oForm.DataSources.DBDataSources.Count; i++)
                {
                    string tName = oForm.DataSources.DBDataSources.Item(i).TableName;
                    if (tName == "ORPC") hasORPC = true;
                    if (tName == "OPCH") hasOPCH = true;
                }
            }
            catch { }

            if (hasORPC)
            {
                tableName = "ORPC";
                tipoDocumento = "NC";
                return;
            }

            if (hasOPCH)
            {
                tableName = "OPCH";
                tipoDocumento = "FACTURA";
                try
                {
                    var ds = oForm.DataSources.DBDataSources.Item("OPCH");
                    string docSubType = ds.GetValue("DocSubType", 0).Trim();
                    if (docSubType.Equals("DN", StringComparison.OrdinalIgnoreCase))
                    {
                        tipoDocumento = "ND";
                    }
                }
                catch { }
                return;
            }

            tableName = "OPCH";
            tipoDocumento = "FACTURA";
        }

        public static void AddValidationButton(SAPbouiCOM.Form oForm)
        {
            if (oForm == null) return;

            try
            {
                // Verificar si el botón ya fue agregado previamente
                try
                {
                    if (oForm.Items.Item("btnVCAE") != null)
                        return;
                }
                catch
                {
                    // Item no existe todavía en el formulario
                }

                SAPbouiCOM.Item oItemRef = oForm.Items.Item("2");
                SAPbouiCOM.Item oItem = oForm.Items.Add("btnVCAE", SAPbouiCOM.BoFormItemTypes.it_BUTTON);
                oItem.Top = oItemRef.Top;
                oItem.Left = oItemRef.Left + oItemRef.Width + 5;
                oItem.Width = 90;
                oItem.Height = oItemRef.Height;
                oItem.FromPane = 0;
                oItem.ToPane = 0;

                SAPbouiCOM.Button btn = (SAPbouiCOM.Button)oItem.Specific;
                btn.Caption = "Validar CAE";
                btn.PressedAfter += (s, e) =>
                {
                    _ = ValidarCaeAFIPAsync(oForm);
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en AddValidationButton para FormType '{oForm.TypeEx}'", ex);
            }
        }

        public static void SBO_Application_ItemEvent(string FormUID, ref SAPbouiCOM.ItemEvent pVal, out bool BubbleEvent)
        {
            BubbleEvent = true;

            try
            {
                if (pVal.BeforeAction) return;

                if (IsSupportedForm(pVal.FormTypeEx, pVal.FormType))
                {
                    if (pVal.EventType == SAPbouiCOM.BoEventTypes.et_FORM_LOAD ||
                        pVal.EventType == SAPbouiCOM.BoEventTypes.et_FORM_VISIBLE ||
                        pVal.EventType == SAPbouiCOM.BoEventTypes.et_FORM_RESIZE)
                    {
                        SAPbouiCOM.Form oForm = Application.SBO_Application.Forms.Item(FormUID);
                        AddValidationButton(oForm);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error en SBO_Application_ItemEvent para FormUID {FormUID}", ex);
            }
        }

        public static async Task ValidarCaeAFIPAsync(SAPbouiCOM.Form oForm)
        {
            if (_isValidating) return;
            _isValidating = true;

            try
            {
                string fCAE = "U_CAE";
                string fPV = "PTICode";
                string fCuit = "LicTradNum";

                string tableName;
                string tipoDocumento;
                DeterminarDocumento(oForm, out tableName, out tipoDocumento);

                SAPbouiCOM.DBDataSource ds = oForm.DataSources.DBDataSources.Item(tableName);

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

                Application.SBO_Application.StatusBar.SetText($"Iniciando validación de CAE ({tipoDocumento})...", SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Warning);

                var requestData = new CAERequest
                {
                    cbteModo = "CAE",
                    cuitEmisor = long.Parse(cuitE),
                    ptoVta = pVta,
                    cbteTipo = letra,
                    cbteNro = long.Parse(cbteNroStr),
                    cbteFch = cbteFch,
                    codAut = codAut,
                    docTipoRecep = "80",
                    docNroRecep = miCuit,
                    tipoDocumento = tipoDocumento,
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

                    if (res != null && res.ResultadoConstatacion == "A")
                    {
                        Application.SBO_Application.StatusBar.SetText($"✅ CAE VÁLIDO ({tipoDocumento}).", SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Success);
                        string finalMsg = string.IsNullOrWhiteSpace(res.Msg) ? "✅ CAE validado correctamente" : $"✅ CAE VALIDADO ({tipoDocumento}): " + res.Msg;
                        Application.SBO_Application.MessageBox(finalMsg);
                    }
                    else
                    {
                        string msgError = res != null ? res.Msg : "Respuesta nula de la API";
                        Logger.Error($"CAE RECHAZADO. Petición: {jsonContent} | Error: {msgError}");
                        Application.SBO_Application.StatusBar.SetText($"❌ CAE RECHAZADO ({tipoDocumento}).", SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Error);
                        Application.SBO_Application.MessageBox($"❌ RECHAZADO ({tipoDocumento}): " + msgError);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error en ValidarCaeAFIPAsync", ex);
                Application.SBO_Application.StatusBar.SetText("Error de validación: " + ex.Message, SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Error);
            }
            finally
            {
                _isValidating = false;
            }
        }

        public static double ParseSAPAmount(string rawValue)
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

        public static string GetMiCuit()
        {
            SAPbobsCOM.Company comp = (SAPbobsCOM.Company)Application.SBO_Application.Company.GetDICompany();
            SAPbobsCOM.Recordset rs = (SAPbobsCOM.Recordset)comp.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
            rs.DoQuery("SELECT \"TaxIdNum\" FROM OADM");
            return rs.EoF ? "" : Convert.ToString(rs.Fields.Item(0).Value);
        }


    }
}