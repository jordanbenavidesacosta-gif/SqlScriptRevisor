using Microsoft.SqlServer.TransactSql.ScriptDom;
using RevisorScripstSQL.Models;

namespace RevisorScripstSQL.Rules.NoLock
{
    public class NoLockVisitor : TSqlFragmentVisitor
    {
        public List<TablaInfo> Tablas { get; } = new();


        private readonly HashSet<int> _lineasExcluidas = new();


        private readonly HashSet<string> _nombresCTE =
            new(StringComparer.OrdinalIgnoreCase);

        public override void Visit(CommonTableExpression node)
        {
            if (!string.IsNullOrEmpty(node.ExpressionName?.Value))
                _nombresCTE.Add(node.ExpressionName.Value);
        }


        public override void Visit(DeleteStatement node)
        {
            if (node.DeleteSpecification?.Target != null)
                MarcarLineas(node.DeleteSpecification.Target);

            if (node.DeleteSpecification?.FromClause != null)
                foreach (var t in node.DeleteSpecification.FromClause.TableReferences)
                    MarcarLineas(t);
        }


        public override void Visit(UpdateStatement node)
        {
            if (node.UpdateSpecification?.Target != null)
                MarcarLineas(node.UpdateSpecification.Target);

            if (node.UpdateSpecification?.FromClause != null)
                foreach (var t in node.UpdateSpecification.FromClause.TableReferences)
                    MarcarLineas(t);
        }

        public override void Visit(QuerySpecification node)
        {
            if (node.FromClause != null)
                foreach (var table in node.FromClause.TableReferences)
                    ProcesarTableReference(table, "FROM");

            base.Visit(node);
        }
        private void MarcarLineas(TableReference tableRef)
        {
            if (tableRef is NamedTableReference named)
            {
                _lineasExcluidas.Add(named.StartLine);
                return;
            }

            if (tableRef is QualifiedJoin join)
            {
                MarcarLineas(join.FirstTableReference);
                MarcarLineas(join.SecondTableReference);
                return;
            }

            if (tableRef is UnqualifiedJoin crossJoin)
            {
                MarcarLineas(crossJoin.FirstTableReference);
                MarcarLineas(crossJoin.SecondTableReference);
            }
        }

        private void ProcesarTableReference(TableReference tableRef, string tipo)
        {
            if (tableRef is NamedTableReference named)
            {
                var nombreBase = named.SchemaObject?.BaseIdentifier?.Value;

                if (!string.IsNullOrEmpty(nombreBase) && _nombresCTE.Contains(nombreBase))
                    return;

                if (_lineasExcluidas.Contains(named.StartLine))
                    return;

                Tablas.Add(new TablaInfo { Tabla = named, Tipo = tipo });
                return;
            }

            if (tableRef is QualifiedJoin join)
            {
                string joinType = join.QualifiedJoinType switch
                {
                    QualifiedJoinType.Inner => "INNER JOIN",
                    QualifiedJoinType.LeftOuter => "LEFT JOIN",
                    QualifiedJoinType.RightOuter => "RIGHT JOIN",
                    QualifiedJoinType.FullOuter => "FULL JOIN",
                    _ => "JOIN"
                };

                ProcesarTableReference(join.FirstTableReference, joinType);
                ProcesarTableReference(join.SecondTableReference, joinType);
                return;
            }

            if (tableRef is UnqualifiedJoin crossJoin)
            {
                ProcesarTableReference(crossJoin.FirstTableReference, "CROSS JOIN");
                ProcesarTableReference(crossJoin.SecondTableReference, "CROSS JOIN");
            }
        }
    }
}
