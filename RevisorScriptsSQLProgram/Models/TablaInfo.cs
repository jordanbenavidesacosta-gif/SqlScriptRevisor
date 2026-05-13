using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace RevisorScripstSQL.Models
{
    public class TablaInfo
    {
        public NamedTableReference Tabla { get; set; }
        public string Tipo { get; set; } 

    }
}