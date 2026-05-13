using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Text;

namespace RevisorScripstSQL.Models
{
    public class AliasReplacementInfo
    {
        public string TablaOriginal { get; set; } = string.Empty;

        public string AliasNuevo { get; set; } = string.Empty;

        public NamedTableReference TablaReferencia { get; set; }
    }
}
