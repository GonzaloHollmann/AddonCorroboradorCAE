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
        public override void OnInitializeComponent() { CaeValidationHelper.AddValidationButton((SAPbouiCOM.Form)this.UIAPIRawForm); }
        public override void OnInitializeFormEvents() { }
    }

    [FormAttribute("181", "SystemForm1.b1f")]
    class SystemFormNC : SystemForm1
    {
        public SystemFormNC() { }
    }

    [FormAttribute("65306", "SystemForm1.b1f")]
    class SystemFormND : SystemForm1
    {
        public SystemFormND() { }
    }

    [FormAttribute("60092", "SystemForm1.b1f")]
    class SystemFormReserva : SystemForm1
    {
        public SystemFormReserva() { }
    }
}
