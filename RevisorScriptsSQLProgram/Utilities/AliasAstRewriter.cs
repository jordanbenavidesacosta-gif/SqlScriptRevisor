using Microsoft.SqlServer.TransactSql.ScriptDom;
using RevisorScripstSQL.Models;

namespace RevisorScripstSQL.Utilities
{
    public class AliasAstRewriter : TSqlFragmentVisitor
    {
        private readonly List<AliasReplacementInfo> _tablas;

        public AliasAstRewriter(List<AliasReplacementInfo> tablas)
        {
            _tablas = tablas;
        }

        public override void Visit(NamedTableReference node)
        {
            var nombreTabla = node.SchemaObject.BaseIdentifier.Value;

            var info = _tablas.FirstOrDefault(t =>
                t.TablaOriginal.Equals(nombreTabla, StringComparison.OrdinalIgnoreCase));

            if (info == null)
                return;

            if (node.Alias == null)
            {
                node.Alias = new Identifier
                {
                    Value = info.AliasNuevo
                };
            }

            base.Visit(node);
        }

        public override void Visit(ColumnReferenceExpression node)
        {
            if (node.MultiPartIdentifier == null)
                return;

            if (node.MultiPartIdentifier.Identifiers.Count < 2)
                return;

            var tabla = node.MultiPartIdentifier.Identifiers[0].Value;

            var info = _tablas.FirstOrDefault(t =>
                t.TablaOriginal.Equals(tabla, StringComparison.OrdinalIgnoreCase));

            if (info == null)
                return;

            node.MultiPartIdentifier.Identifiers[0].Value = info.AliasNuevo;

            base.Visit(node);
        }
    }
}