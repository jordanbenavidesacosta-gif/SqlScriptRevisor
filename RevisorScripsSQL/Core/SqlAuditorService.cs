using Microsoft.SqlServer.TransactSql.ScriptDom;
using RevisorScripstSQL.Models;
using RevisorScripstSQL.Rules.DDL;
using RevisorScripstSQL.Rules.DDL.CreateTables;
using RevisorScripstSQL.Rules.Interfaces;
using RevisorScripstSQL.Rules.NoLock;
using RevisorScripstSQL.Rules.StoredProcedures;
using RevisorScripstSQL.Rules.StoreProcedures;
using RevisorScripstSQL.Rules.StoreProcedures.Alias;
using RevisorScripstSQL.Rules.Triggers;
using RevisorScripstSQL.Utilities;
using System.IO;
using System.Text.RegularExpressions;

namespace RevisorScripstSQL.Core
{
    public class SqlAuditorService
    {
        private readonly List<ISqlRules> reglas;

        public SqlAuditorService()
        {
            reglas = new List<ISqlRules>
        {
            new UseRule(),
            new StoredProcedureHeaderRuleDom(),
            new StoredProcedureRule(),
            new CreateTableRule(),
            new PrimaryKeyRule(),
            new AliasRule(),
            new NoLockRule(),
            new TriggerRule()
        };
        }

        public List<SqlError> Auditar(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
                return new List<SqlError>();

            var lineas = script.Split('\n');

            var errores = reglas
                .SelectMany(r => r.Validate(lineas))
                .OrderBy(e => e.Linea)
                .ToList();

            return errores;
        }
    }
}