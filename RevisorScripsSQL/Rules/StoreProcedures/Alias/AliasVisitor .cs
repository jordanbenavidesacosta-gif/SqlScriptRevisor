using Microsoft.SqlServer.TransactSql.ScriptDom;
using RevisorScripstSQL.Models;

namespace RevisorScripstSQL.Rules.StoreProcedures.Alias
{
    public class AliasVisitor : TSqlFragmentVisitor
    {
        public List<AliasReplacementInfo> TablasSinAlias { get; } = new();

        private readonly HashSet<string> _aliasesUsados = new();

        public override void Visit(QualifiedJoin node)
        {
            ProcesarTabla(node.FirstTableReference);
            ProcesarTabla(node.SecondTableReference);

            base.Visit(node);
        }

        public override void Visit(UnqualifiedJoin node)
        {
            ProcesarTabla(node.FirstTableReference);
            ProcesarTabla(node.SecondTableReference);

            base.Visit(node);
        }

        private void ProcesarTabla(TableReference tableRef)
        {
            if (tableRef is not NamedTableReference node)
                return;

            if (node.Alias != null)
            {
                _aliasesUsados.Add(node.Alias.Value.ToLower());
                return;
            }

            if (node.SchemaObject.SchemaIdentifier != null)
            {
                var schema = node.SchemaObject.SchemaIdentifier.Value;

                if (schema.Equals("sys", StringComparison.OrdinalIgnoreCase) ||
                    schema.Equals("INFORMATION_SCHEMA", StringComparison.OrdinalIgnoreCase))
                    return;
            }

            var nombreTabla = node.SchemaObject.BaseIdentifier.Value;

            var alias = GenerarAlias(nombreTabla);

            TablasSinAlias.Add(new AliasReplacementInfo
            {
                TablaOriginal = nombreTabla,
                AliasNuevo = alias,
                TablaReferencia = node
            });
        }

        private string GenerarAlias(string tabla)
        {
            var nombre = tabla;

            if (nombre.Contains("_"))
                nombre = nombre.Split('_')[0];

            nombre = nombre.ToLower();

            string alias3 = nombre.Length >= 3
                ? nombre.Substring(0, 3)
                : nombre;

            if (!_aliasesUsados.Contains(alias3))
            {
                _aliasesUsados.Add(alias3);
                return alias3;
            }

            string alias2 = nombre.Length >= 2
                ? nombre.Substring(0, 2)
                : nombre;

            if (!_aliasesUsados.Contains(alias2))
            {
                _aliasesUsados.Add(alias2);
                return alias2;
            }

            int contador = 1;
            string aliasFinal = alias2;

            while (_aliasesUsados.Contains(aliasFinal))
            {
                aliasFinal = alias2 + contador;
                contador++;
            }

            _aliasesUsados.Add(aliasFinal);

            return aliasFinal;
        }
    }
}