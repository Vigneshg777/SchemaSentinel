using Microsoft.SqlServer.TransactSql.ScriptDom;
using SchemaSentinel.Application.Interfaces;
using SchemaSentinel.Domain.Parsing;

namespace SchemaSentinel.Infrastructure.Parsing;

/// <summary>
/// Parses migration scripts using the ScriptDom AST. The script is only ever
/// tokenised and walked — it is never executed against any database.
/// </summary>
public sealed class ScriptDomMigrationParser : IMigrationParser
{
    public ParsedMigration Parse(string script)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);

        using var reader = new StringReader(script ?? string.Empty);
        var fragment = parser.Parse(reader, out IList<Microsoft.SqlServer.TransactSql.ScriptDom.ParseError> parseErrors);

        var errors = parseErrors
            .Select(e => new Domain.Parsing.ParseError(e.Line, e.Column, e.Message))
            .ToList();

        var visitor = new OperationVisitor();
        fragment?.Accept(visitor);

        return new ParsedMigration
        {
            Operations = visitor.Operations,
            ReferencedObjects = visitor.ReferencedObjects
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Errors = errors
        };
    }

    private sealed class OperationVisitor : TSqlFragmentVisitor
    {
        public List<MigrationOperation> Operations { get; } = [];
        public HashSet<string> ReferencedObjects { get; } = new(StringComparer.OrdinalIgnoreCase);

        public override void Visit(DropTableStatement node)
        {
            foreach (var obj in node.Objects)
            {
                var table = GetName(obj);
                Reference(table);
                Operations.Add(new DropTableOperation
                {
                    TableName = table,
                    RawStatement = GetText(node)
                });
            }
        }

        public override void Visit(AlterTableDropTableElementStatement node)
        {
            var table = GetName(node.SchemaObjectName);
            Reference(table);

            foreach (var element in node.AlterTableDropTableElements)
            {
                if (element.TableElementType == TableElementType.Column && element.Name is not null)
                {
                    Operations.Add(new DropColumnOperation
                    {
                        TableName = table,
                        ColumnName = element.Name.Value,
                        RawStatement = GetText(node)
                    });
                }
            }
        }

        public override void Visit(AlterTableAlterColumnStatement node)
        {
            var table = GetName(node.SchemaObjectName);
            Reference(table);

            var (dataType, length, isMax) = ExtractDataType(node.DataType);

            Operations.Add(new AlterColumnOperation
            {
                TableName = table,
                ColumnName = node.ColumnIdentifier?.Value ?? string.Empty,
                DataType = dataType,
                Length = length,
                IsMaxLength = isMax,
                Nullability = DetectNullability(node),
                RawStatement = GetText(node)
            });
        }

        public override void Visit(AlterTableAddTableElementStatement node)
        {
            var table = GetName(node.SchemaObjectName);
            Reference(table);

            if (node.Definition is null)
            {
                return;
            }

            foreach (var column in node.Definition.ColumnDefinitions)
            {
                var (dataType, length, isMax) = ExtractDataType(column.DataType);
                Operations.Add(new AddColumnOperation
                {
                    TableName = table,
                    ColumnName = column.ColumnIdentifier?.Value ?? string.Empty,
                    DataType = dataType,
                    Length = length,
                    IsMaxLength = isMax,
                    Nullability = DetectColumnNullability(column),
                    HasDefault = column.Constraints.OfType<DefaultConstraintDefinition>().Any()
                        || column.DefaultConstraint is not null,
                    RawStatement = GetText(node)
                });
            }

            foreach (var fk in node.Definition.TableConstraints.OfType<ForeignKeyConstraintDefinition>())
            {
                AddForeignKey(table, fk, GetText(node));
            }

            foreach (var column in node.Definition.ColumnDefinitions)
            {
                foreach (var fk in column.Constraints.OfType<ForeignKeyConstraintDefinition>())
                {
                    AddForeignKey(table, fk, GetText(node));
                }
            }
        }

        public override void Visit(CreateTableStatement node)
        {
            var table = GetName(node.SchemaObjectName);
            Reference(table);

            if (node.Definition is null)
            {
                return;
            }

            foreach (var fk in node.Definition.TableConstraints.OfType<ForeignKeyConstraintDefinition>())
            {
                AddForeignKey(table, fk, GetText(node));
            }

            foreach (var column in node.Definition.ColumnDefinitions)
            {
                foreach (var fk in column.Constraints.OfType<ForeignKeyConstraintDefinition>())
                {
                    AddForeignKey(table, fk, GetText(node));
                }
            }
        }

        public override void Visit(CreateIndexStatement node)
        {
            var table = GetName(node.OnName);
            Reference(table);
            Operations.Add(new IndexOperation
            {
                Kind = IndexOperationKind.Create,
                IndexName = node.Name?.Value,
                TableName = table,
                RawStatement = GetText(node)
            });
        }

        public override void Visit(DropIndexStatement node)
        {
            foreach (var clause in node.DropIndexClauses.OfType<DropIndexClause>())
            {
                var table = clause.Object is not null ? GetName(clause.Object) : null;
                if (table is not null)
                {
                    Reference(table);
                }

                Operations.Add(new IndexOperation
                {
                    Kind = IndexOperationKind.Drop,
                    IndexName = clause.Index?.Value,
                    TableName = table,
                    RawStatement = GetText(node)
                });
            }
        }

        public override void Visit(AlterIndexStatement node)
        {
            var table = node.OnName is not null ? GetName(node.OnName) : null;
            if (table is not null)
            {
                Reference(table);
            }

            Operations.Add(new IndexOperation
            {
                Kind = IndexOperationKind.Alter,
                IndexName = node.Name?.Value,
                TableName = table,
                RawStatement = GetText(node)
            });
        }

        private void AddForeignKey(string table, ForeignKeyConstraintDefinition fk, string rawText)
        {
            var referenced = fk.ReferenceTableName is not null ? GetName(fk.ReferenceTableName) : null;
            if (referenced is not null)
            {
                Reference(referenced);
            }

            Operations.Add(new ForeignKeyOperation
            {
                TableName = table,
                ReferencedTable = referenced,
                Columns = fk.Columns.Select(c => c.Value).ToList(),
                RawStatement = rawText
            });
        }

        private void Reference(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                ReferencedObjects.Add(name);
            }
        }

        private static string GetName(SchemaObjectName? name)
            => name?.BaseIdentifier?.Value ?? string.Empty;

        private static (string? type, int? length, bool isMax) ExtractDataType(DataTypeReference? dataType)
        {
            if (dataType is not SqlDataTypeReference sqlType)
            {
                return (null, null, false);
            }

            var typeName = sqlType.Name?.BaseIdentifier?.Value;

            foreach (var parameter in sqlType.Parameters)
            {
                if (parameter is MaxLiteral)
                {
                    return (typeName, null, true);
                }

                if (parameter is IntegerLiteral integer
                    && int.TryParse(integer.Value, out var value))
                {
                    return (typeName, value, false);
                }
            }

            return (typeName, null, false);
        }

        private static ColumnNullability DetectColumnNullability(ColumnDefinition column)
        {
            var nullable = column.Constraints.OfType<NullableConstraintDefinition>().FirstOrDefault();
            if (nullable is null)
            {
                return ColumnNullability.Unspecified;
            }

            return nullable.Nullable ? ColumnNullability.Nullable : ColumnNullability.NotNull;
        }

        /// <summary>
        /// Nullability for ALTER COLUMN is detected from the statement's token
        /// range, which reliably captures the "NOT NULL" / "NULL" clause.
        /// </summary>
        private static ColumnNullability DetectNullability(TSqlFragment node)
        {
            var tokens = node.ScriptTokenStream;
            if (tokens is null)
            {
                return ColumnNullability.Unspecified;
            }

            for (var i = node.FirstTokenIndex; i <= node.LastTokenIndex && i < tokens.Count; i++)
            {
                if (tokens[i].TokenType != TSqlTokenType.Null)
                {
                    continue;
                }

                for (var j = i - 1; j >= node.FirstTokenIndex; j--)
                {
                    var type = tokens[j].TokenType;
                    if (type is TSqlTokenType.WhiteSpace
                        or TSqlTokenType.SingleLineComment
                        or TSqlTokenType.MultilineComment)
                    {
                        continue;
                    }

                    return type == TSqlTokenType.Not
                        ? ColumnNullability.NotNull
                        : ColumnNullability.Nullable;
                }

                return ColumnNullability.Nullable;
            }

            return ColumnNullability.Unspecified;
        }

        private static string GetText(TSqlFragment fragment)
        {
            var tokens = fragment.ScriptTokenStream;
            if (tokens is null || fragment.FirstTokenIndex < 0)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();
            for (var i = fragment.FirstTokenIndex; i <= fragment.LastTokenIndex && i < tokens.Count; i++)
            {
                builder.Append(tokens[i].Text);
            }

            return builder.ToString().Trim();
        }
    }
}
