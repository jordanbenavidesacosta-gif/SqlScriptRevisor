using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace RevisorScripstSQL.Utilities
{

    public class NoLockAstRewriter : TSqlFragmentVisitor
    {

        private readonly HashSet<string> _nombresCTE =
            new(StringComparer.OrdinalIgnoreCase);


        private readonly HashSet<int> _lineasDeleteUpdate = new();


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
                    ProcesarTableReference(table);

            base.Visit(node);
        }

        private void ProcesarTableReference(TableReference tableRef)
        {
            if (tableRef is NamedTableReference named)
            {
                AgregarNoLockSiAplica(named);
                return;
            }

            if (tableRef is QualifiedJoin join)
            {
                ProcesarTableReference(join.FirstTableReference);
                ProcesarTableReference(join.SecondTableReference);
                return;
            }

            if (tableRef is UnqualifiedJoin crossJoin)
            {
                ProcesarTableReference(crossJoin.FirstTableReference);
                ProcesarTableReference(crossJoin.SecondTableReference);
            }
        }

        private void AgregarNoLockSiAplica(NamedTableReference tabla)
        {
            var nombre = tabla.SchemaObject?.BaseIdentifier?.Value;

            if (string.IsNullOrEmpty(nombre)) return;
            if (_nombresCTE.Contains(nombre)) return; 
            if (_lineasDeleteUpdate.Contains(tabla.StartLine)) return; 
            if (nombre.StartsWith("#")) return; 
            if (tabla.TableHints != null && tabla.TableHints.Count > 0) return; 

            var schema = tabla.SchemaObject?.SchemaIdentifier?.Value;
            if (schema?.Equals("sys", StringComparison.OrdinalIgnoreCase) == true) return;
            if (schema?.Equals("INFORMATION_SCHEMA", StringComparison.OrdinalIgnoreCase) == true) return;


            tabla.TableHints.Add(new TableHint { HintKind = TableHintKind.NoLock });
        }

        private void MarcarLineas(TableReference tableRef)
        {
            if (tableRef is NamedTableReference named)
            {
                _lineasDeleteUpdate.Add(named.StartLine);
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
    }
}
