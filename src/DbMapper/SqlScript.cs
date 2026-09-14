using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DbMapper;

// ScriptDom handles T-SQL syntax. Only explicitly supported AST nodes change the metadata model.
internal sealed class SqlScript(SchemaModel model, string? database, CancellationToken cancellationToken)
{
    private string? scriptDatabase = database;
    private readonly Dictionary<(int Table, string Name), string> defaults = [];
    private int nextId = model.Relations.Select(r => r.Id).Concat(model.Routines.Select(r => r.Id)).DefaultIfEmpty().Max();

    public static void Apply(SchemaModel model, string sql, string? database, CancellationToken cancellationToken)
    {
        var tree = new TSql180Parser(true).Parse(new StringReader(sql), out var errors);
        if (errors.Count > 0)
            throw new UsageException($"SQL could not be parsed at line {errors[0].Line}, column {errors[0].Column}. SQL text and parser details are withheld. The bundle was preserved.");
        var update = new SqlScript(model, database, cancellationToken);
        foreach (var batch in ((TSqlScript)tree).Batches)
            foreach (var statement in batch.Statements) update.Apply(statement);
    }

    private void Apply(TSqlStatement statement)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (statement)
        {
            case BeginEndBlockStatement block:
                foreach (var child in block.StatementList.Statements) Apply(child);
                break;
            case IfStatement conditional:
                var branch = Evaluate(conditional.Predicate) ? conditional.ThenStatement : conditional.ElseStatement;
                if (branch is not null) Apply(branch);
                break;
            case UseStatement use:
                if (scriptDatabase is not null && scriptDatabase != use.DatabaseName.Value)
                    throw Error(use, "The script changes database scope. Apply one database at a time with the matching --update-database name.");
                scriptDatabase = use.DatabaseName.Value;
                break;
            case CreateSchemaStatement schema:
                if (schema.StatementList is not null)
                    foreach (var child in schema.StatementList.Statements) Apply(child);
                break; // Empty schemas are outside the catalog export's coverage too.
            case CreateTableStatement create:
                if (create.AsEdge || create.AsNode || create.AsFileTable || create.Options.Count > 0 || create.SelectStatement is not null || create.CloneSource is not null)
                    throw Unsupported(create);
                RequireUnpartitioned(create.OnFileGroupOrPartitionScheme);
                var (schemaName, name) = Name(create.SchemaObjectName);
                Require(!ObjectExists(schemaName, name), create, "CREATE TABLE targets an existing object. Check the starting bundle.");
                var relation = new Relation(++nextId, schemaName, name, "Table", "NON_TEMPORAL_TABLE");
                model.Relations.Add(relation);
                Add(relation, create.Definition, false);
                break;
            case AlterTableAddTableElementStatement add:
                Add(Table(add.SchemaObjectName), add.Definition, add.ExistingRowsCheckEnforcement == ConstraintEnforcement.NoCheck);
                break;
            case AlterTableAlterColumnStatement alter:
                AlterColumn(alter);
                break;
            case AlterTableDropTableElementStatement drop:
                DropElements(drop);
                break;
            case AlterTableConstraintModificationStatement constraints:
                SetConstraints(constraints);
                break;
            case CreateIndexStatement index:
                RequireUnpartitioned(index.OnFileGroupOrPartitionScheme);
                AddIndex(Relation(index.OnName), index.Name.Value, index.Unique, index.Clustered == true ? "CLUSTERED" : "NONCLUSTERED",
                    index.Columns, index.IncludeColumns, index.FilterPredicate is not null, index);
                break;
            case DropIndexStatement drop:
                foreach (var item in drop.DropIndexClauses)
                {
                    if (item is not DropIndexClause clause) throw Unsupported(item);
                    var table = Relation(clause.Object);
                    var found = table.Indexes.SingleOrDefault(i => i.Name == clause.Index.Value);
                    Require(found is not null || drop.IsIfExists, drop, "DROP INDEX targets an unknown index.");
                    Require(found is null || (!found.PrimaryKey && !found.UniqueConstraint), drop, "Use DROP CONSTRAINT for a primary or unique key.");
                    if (found is not null) table.Indexes.Remove(found);
                }
                break;
            case AlterIndexStatement alter:
                var indexed = Relation(alter.OnName);
                if (alter.Partition is not null || alter.AlterIndexType is not (AlterIndexType.Disable or AlterIndexType.Rebuild or AlterIndexType.Reorganize)) throw Unsupported(alter);
                var indexes = indexed.Indexes.Where(i => alter.All || i.Name == alter.Name.Value).ToList();
                Require(indexes.Count > 0, alter, "ALTER INDEX targets an unknown index.");
                foreach (var index in indexes)
                {
                    // Disabling a clustered/unique index can also disable related keys and storage.
                    if (alter.AlterIndexType == AlterIndexType.Disable && (index.Unique || index.Kind == "CLUSTERED")) throw Unsupported(alter);
                    if (alter.AlterIndexType != AlterIndexType.Reorganize)
                        indexed.Indexes[indexed.Indexes.IndexOf(index)] = index with { Disabled = alter.AlterIndexType == AlterIndexType.Disable };
                }
                break;
            case DropTableStatement drop: DropRelations(drop, "Table"); break;
            case DropViewStatement drop: DropRelations(drop, "View"); break;
            case ProcedureStatementBody procedure: Routine(procedure); break;
            case FunctionStatementBody function: Routine(function); break;
            case DropProcedureStatement drop: DropRoutines(drop, true); break;
            case DropFunctionStatement drop: DropRoutines(drop, false); break;
            case ViewStatementBody view: View(view); break;
            case TriggerStatementBody trigger: Trigger(trigger); break;
            case DropTriggerStatement drop:
                if (drop.TriggerScope != TriggerScope.Normal) throw Unsupported(drop);
                foreach (var item in drop.Objects)
                {
                    var triggerName = Name(item);
                    var owners = model.Relations.Where(r => r.Schema == triggerName.Schema && r.Triggers.Any(t => t.Name == triggerName.Name)).ToList();
                    Require(owners.Count == 1 || (owners.Count == 0 && drop.IsIfExists), drop, "DROP TRIGGER targets an unknown or ambiguous trigger.");
                    foreach (var owner in owners) owner.Triggers.RemoveAll(t => t.Name == triggerName.Name);
                }
                break;
            case PredicateSetStatement setting:
                const SetOptions harmless = SetOptions.AnsiNulls | SetOptions.QuotedIdentifier | SetOptions.NoCount | SetOptions.XactAbort
                    | SetOptions.AnsiPadding | SetOptions.AnsiWarnings | SetOptions.ArithAbort | SetOptions.ConcatNullYieldsNull | SetOptions.NumericRoundAbort;
                if ((setting.Options & ~harmless) != 0) throw Unsupported(setting);
                break;
            case BeginTransactionStatement or CommitTransactionStatement or PrintStatement or DeclareVariableStatement or SetVariableStatement:
            case InsertStatement or UpdateStatement or DeleteStatement or MergeStatement or TruncateTableStatement:
                break; // Data and expression text are not part of the metadata model; nothing is executed.
            case SelectStatement select when select.Into is null:
                break;
            default: throw Unsupported(statement);
        }
    }

    private void Add(Relation table, TableDefinition definition, bool untrusted)
    {
        if (definition.SystemTimePeriod is not null) throw Unsupported(definition);
        foreach (var column in definition.ColumnDefinitions)
        {
            if (column.ComputedColumnExpression is not null || column.GeneratedAlways is not null || column.IsHidden
                || column.StorageOptions?.SparseOption == SparseColumnOption.ColumnSetForAllSparseColumns) throw Unsupported(column);
            Require(table.Columns.All(c => c.Name != column.ColumnIdentifier.Value), column, "ADD targets an existing column. Check the starting bundle.");
            var primary = column.Constraints.OfType<UniqueConstraintDefinition>().Any(c => c.IsPrimaryKey)
                || definition.TableConstraints.OfType<UniqueConstraintDefinition>().Any(c => c.IsPrimaryKey && c.Columns.Any(x => ColumnName(x.Column) == column.ColumnIdentifier.Value));
            var nullable = column.Constraints.OfType<NullableConstraintDefinition>().SingleOrDefault()?.Nullable;
            Require(nullable is not null || primary || column.IdentityOptions is not null, column, "Specify NULL or NOT NULL for new columns; offline updates cannot read database session defaults.");
            Require(!primary || nullable != true, column, "A primary key column cannot be nullable.");
            table.Columns.Add(new(table.Columns.Select(c => c.Ordinal).DefaultIfEmpty().Max() + 1, column.ColumnIdentifier.Value,
                Type(column.DataType), nullable ?? false, column.IdentityOptions is not null, false, false,
                column.StorageOptions?.SparseOption == SparseColumnOption.Sparse, false, "NOT_APPLICABLE", column.Collation?.Value));
        }
        foreach (var column in definition.ColumnDefinitions)
        {
            foreach (var constraint in column.Constraints) Constraint(table, constraint, untrusted, column.ColumnIdentifier.Value);
            if (column.DefaultConstraint is not null) Constraint(table, column.DefaultConstraint, untrusted, column.ColumnIdentifier.Value);
            if (column.Index is not null) InlineIndex(table, column.Index);
        }
        foreach (var constraint in definition.TableConstraints) Constraint(table, constraint, untrusted);
        foreach (var index in definition.Indexes) InlineIndex(table, index);
    }

    private void Constraint(Relation table, ConstraintDefinition constraint, bool untrusted, string? column = null)
    {
        if (constraint is NullableConstraintDefinition) return;
        var name = constraint.ConstraintIdentifier?.Value;
        if (constraint is DefaultConstraintDefinition value)
        {
            var target = Column(table, column ?? value.Column?.Value ?? throw Unsupported(value));
            Require(!target.HasDefault, constraint, "This column already has a default constraint.");
            table.Columns[table.Columns.IndexOf(target)] = target with { HasDefault = true };
            if (name is not null) defaults[(table.Id, name)] = target.Name;
            return;
        }
        Require(name is not null, constraint, "Name primary, unique, foreign key and check constraints explicitly; SQL Server-generated names cannot be determined offline.");
        Require(!table.Indexes.Any(i => i.Name == name) && !table.ForeignKeys.Any(k => k.Name == name) && !table.Checks.Any(c => c.Name == name),
            constraint, "A constraint with this name already exists.");
        switch (constraint)
        {
            case UniqueConstraintDefinition unique:
                RequireUnpartitioned(unique.OnFileGroupOrPartitionScheme);
                if (unique.IndexType?.IndexTypeKind is not (null or IndexTypeKind.Clustered or IndexTypeKind.NonClustered)) throw Unsupported(unique);
                var names = column is null ? unique.Columns.Select(c => (Name: ColumnName(c.Column), Desc: c.SortOrder == SortOrder.Descending)).ToList() : [(Name: column, Desc: false)];
                Require(!unique.IsPrimaryKey || table.Indexes.All(i => !i.PrimaryKey), unique, "The table already has a primary key.");
                var clustered = unique.Clustered ?? (unique.IsPrimaryKey && table.Indexes.All(i => i.Kind != "CLUSTERED"));
                Require(!clustered || table.Indexes.All(i => i.Kind != "CLUSTERED"), unique, "The table already has a clustered index.");
                var index = new DbIndex(++nextId, name!, clustered ? "CLUSTERED" : "NONCLUSTERED", true, unique.IsPrimaryKey, !unique.IsPrimaryKey, false, false);
                foreach (var item in names)
                {
                    var c = Column(table, item.Name);
                    Require(!unique.IsPrimaryKey || !c.Nullable, unique, "A primary key column cannot be nullable.");
                    index.Columns.Add(new(index.Columns.Count + 1, c.Name, index.Columns.Count + 1, item.Desc, false, 0));
                }
                table.Indexes.Add(index);
                break;
            case ForeignKeyConstraintDefinition foreign:
                var target = Table(foreign.ReferenceTableName);
                var sources = column is null ? foreign.Columns.Select(c => c.Value).ToList() : [column];
                var targets = foreign.ReferencedTableColumns.Select(c => c.Value).ToList();
                if (targets.Count == 0)
                    targets = target.Indexes.SingleOrDefault(i => i.PrimaryKey)?.Columns.OrderBy(c => c.KeyOrdinal).Select(c => c.Name).ToList() ?? [];
                Require(sources.Count > 0 && sources.Count == targets.Count, foreign, "Foreign key columns could not be resolved from the bundle.");
                var key = new ForeignKey(++nextId, name!, target.Id, target.Schema, target.Name, Action(foreign.DeleteAction), Action(foreign.UpdateAction), false, untrusted);
                for (var i = 0; i < sources.Count; i++) key.Columns.Add(new(i + 1, Column(table, sources[i]).Name, Column(target, targets[i]).Name));
                table.ForeignKeys.Add(key);
                break;
            case CheckConstraintDefinition:
                table.Checks.Add(new(name!, false, untrusted));
                break;
            default: throw Unsupported(constraint);
        }
    }

    private void InlineIndex(Relation table, IndexDefinition index)
    {
        RequireUnpartitioned(index.OnFileGroupOrPartitionScheme);
        var kind = index.IndexType?.IndexTypeKind;
        if (kind is not (null or IndexTypeKind.Clustered or IndexTypeKind.NonClustered)) throw Unsupported(index);
        AddIndex(table, index.Name.Value, index.Unique, kind == IndexTypeKind.Clustered ? "CLUSTERED" : "NONCLUSTERED",
            index.Columns, index.IncludeColumns, index.FilterPredicate is not null, index);
    }

    private void AddIndex(Relation table, string name, bool unique, string kind, IEnumerable<ColumnWithSortOrder> columns,
        IEnumerable<ColumnReferenceExpression> includes, bool filtered, TSqlFragment fragment)
    {
        Require(table.Indexes.All(i => i.Name != name), fragment, "An index with this name already exists.");
        Require(kind != "CLUSTERED" || table.Indexes.All(i => i.Kind != "CLUSTERED"), fragment, "The table already has a clustered index.");
        var index = new DbIndex(++nextId, name, kind, unique, false, false, filtered, false);
        foreach (var item in columns)
            index.Columns.Add(new(index.Columns.Count + 1, Column(table, ColumnName(item.Column)).Name, index.Columns.Count + 1, item.SortOrder == SortOrder.Descending, false, 0));
        foreach (var item in includes) index.Columns.Add(new(index.Columns.Count + 1, Column(table, ColumnName(item)).Name, 0, false, true, 0));
        Require(index.Columns.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count() == index.Columns.Count, fragment, "Index columns must not repeat.");
        table.Indexes.Add(index);
    }

    private void AlterColumn(AlterTableAlterColumnStatement statement)
    {
        var table = Table(statement.SchemaObjectName);
        var old = Column(table, statement.ColumnIdentifier.Value);
        if (statement.DataType is null || statement.AlterTableAlterColumnOption is not (AlterTableAlterColumnOption.Null or AlterTableAlterColumnOption.NotNull)
            || statement.GeneratedAlways is not null || old.Computed) throw Unsupported(statement);
        var nullable = statement.AlterTableAlterColumnOption == AlterTableAlterColumnOption.Null;
        Require(!nullable || !table.Indexes.Any(i => i.PrimaryKey && i.Columns.Any(c => c.Name == old.Name)), statement, "A primary key column cannot be nullable.");
        table.Columns[table.Columns.IndexOf(old)] = old with { Type = Type(statement.DataType), Nullable = nullable, Collation = statement.Collation?.Value ?? old.Collation };
    }

    private void DropElements(AlterTableDropTableElementStatement statement)
    {
        var table = Table(statement.SchemaObjectName);
        foreach (var item in statement.AlterTableDropTableElements)
        {
            var name = item.Name.Value;
            if (item.TableElementType == TableElementType.Column)
            {
                var column = table.Columns.SingleOrDefault(c => c.Name == name);
                if (column is null && item.IsIfExists) continue;
                Require(column is not null, item, "DROP COLUMN targets an unknown column.");
                Require(!table.Indexes.Any(i => i.Columns.Any(c => c.Name == name)) && !table.ForeignKeys.Any(k => k.Columns.Any(c => c.Source == name))
                    && !model.Relations.SelectMany(r => r.ForeignKeys).Any(k => k.TargetId == table.Id && k.Columns.Any(c => c.Target == name)),
                    item, "Drop dependent indexes and foreign keys before dropping a column.");
                Require(!column!.HasDefault && table.Checks.Count == 0, item, "Remove defaults and checks before dropping a column; their expression dependencies are omitted from the bundle.");
                table.Columns.Remove(column);
            }
            else if (item.TableElementType == TableElementType.Constraint)
            {
                var index = table.Indexes.SingleOrDefault(i => i.Name == name && (i.PrimaryKey || i.UniqueConstraint));
                if (index is not null)
                {
                    Require(!model.Relations.SelectMany(r => r.ForeignKeys).Any(k => k.TargetId == table.Id && k.Columns.Select(c => c.Target)
                        .SequenceEqual(index.Columns.OrderBy(c => c.KeyOrdinal).Select(c => c.Name))), item, "Drop referencing foreign keys before dropping a key constraint.");
                    table.Indexes.Remove(index);
                }
                else if (table.ForeignKeys.RemoveAll(k => k.Name == name) + table.Checks.RemoveAll(c => c.Name == name) > 0) { }
                else if (defaults.Remove((table.Id, name), out var defaultColumn))
                {
                    var old = Column(table, defaultColumn);
                    table.Columns[table.Columns.IndexOf(old)] = old with { HasDefault = false };
                }
                else Require(item.IsIfExists && table.Columns.All(c => !c.HasDefault), item,
                    "The constraint was not found. Existing default-constraint names are not in catalog bundles; they cannot be dropped offline by name.");
            }
            else throw Unsupported(item);
        }
    }

    private void SetConstraints(AlterTableConstraintModificationStatement statement)
    {
        var table = Table(statement.SchemaObjectName);
        var names = statement.ConstraintNames.Select(n => n.Value).ToHashSet(StringComparer.Ordinal);
        Require(statement.All || names.All(n => table.ForeignKeys.Any(k => k.Name == n) || table.Checks.Any(c => c.Name == n)), statement, "CHECK/NOCHECK targets an unknown constraint.");
        var disabled = statement.ConstraintEnforcement == ConstraintEnforcement.NoCheck;
        bool Trust(bool previous) => disabled || (statement.ExistingRowsCheckEnforcement != ConstraintEnforcement.Check && previous);
        for (var i = 0; i < table.ForeignKeys.Count; i++)
        {
            var key = table.ForeignKeys[i];
            if (statement.All || names.Contains(key.Name)) table.ForeignKeys[i] = key with { Disabled = disabled, Untrusted = Trust(key.Untrusted) };
        }
        for (var i = 0; i < table.Checks.Count; i++)
        {
            var check = table.Checks[i];
            if (statement.All || names.Contains(check.Name)) table.Checks[i] = check with { Disabled = disabled, Untrusted = Trust(check.Untrusted) };
        }
    }

    private void DropRelations(DropObjectsStatement statement, string kind)
    {
        foreach (var name in statement.Objects)
        {
            var (schema, value) = Name(name);
            var table = model.Relations.SingleOrDefault(r => r.Schema == schema && r.Name == value && r.Kind == kind);
            if (table is null && statement.IsIfExists) continue;
            Require(table is not null, statement, "DROP targets an unknown table or view.");
            Require(!model.Relations.Any(r => r != table && r.ForeignKeys.Any(k => k.TargetId == table!.Id)), statement, "Drop referencing foreign keys before dropping a table.");
            model.Relations.Remove(table!);
        }
    }

    private void Routine(ProcedureStatementBodyBase statement)
    {
        if (statement.MethodSpecifier is not null) throw Unsupported(statement);
        var procedure = statement as ProcedureStatementBody;
        var function = statement as FunctionStatementBody;
        var (schema, name) = Name(procedure?.ProcedureReference.Name ?? function!.Name);
        var existing = model.Routines.SingleOrDefault(r => r.Schema == schema && r.Name == name);
        var kind = procedure is not null ? "SQL_STORED_PROCEDURE" : function!.ReturnType switch
        {
            ScalarFunctionReturnType => "SQL_SCALAR_FUNCTION",
            TableValuedFunctionReturnType => "SQL_TABLE_VALUED_FUNCTION",
            SelectFunctionReturnType => "SQL_INLINE_TABLE_VALUED_FUNCTION",
            _ => throw Unsupported(statement)
        };
        CheckCreateAlter(statement, existing is not null);
        Require(!model.Relations.Any(r => r.Schema == schema && r.Name == name) && (existing is null || existing.Kind == kind), statement, "The routine name or kind conflicts with an existing object.");
        var routine = new Routine(existing?.Id ?? ++nextId, schema, name, kind);
        if (function?.ReturnType is ScalarFunctionReturnType returns) routine.Parameters.Add(new(0, "", Type(returns.DataType), true, false));
        foreach (var parameter in statement.Parameters)
            routine.Parameters.Add(new(routine.Parameters.Count(p => p.Ordinal > 0) + 1, parameter.VariableName.Value, Type(parameter.DataType),
                parameter.Modifier == ParameterModifier.Output, parameter.Modifier == ParameterModifier.ReadOnly));
        if (existing is not null) model.Routines.Remove(existing);
        model.Routines.Add(routine);
    }

    private void DropRoutines(DropObjectsStatement statement, bool procedure)
    {
        foreach (var item in statement.Objects)
        {
            var (schema, name) = Name(item);
            var count = model.Routines.RemoveAll(r => r.Schema == schema && r.Name == name && r.Kind.Contains("PROCEDURE", StringComparison.Ordinal) == procedure);
            Require(count == 1 || statement.IsIfExists, statement, "DROP targets an unknown routine.");
        }
    }

    private void View(ViewStatementBody statement)
    {
        if (statement.IsMaterialized) throw Unsupported(statement);
        var (schema, name) = Name(statement.SchemaObjectName);
        var old = model.Relations.SingleOrDefault(r => r.Schema == schema && r.Name == name);
        CheckCreateAlter(statement, old is not null);
        Require((old is null || old.Kind == "View") && !model.Routines.Any(r => r.Schema == schema && r.Name == name), statement, "The view name conflicts with an existing object.");
        if (statement.SelectStatement.QueryExpression is not QuerySpecification query || query.FromClause?.TableReferences.Count != 1
            || query.FromClause.TableReferences[0] is not NamedTableReference source || query.GroupByClause is not null) throw Unsupported(statement);
        var table = Relation(source.SchemaObject);
        var view = new Relation(old?.Id ?? ++nextId, schema, name, "View", "NOT_APPLICABLE");
        if (old is not null) view.Triggers.AddRange(old.Triggers);
        foreach (var element in query.SelectElements)
        {
            if (element is SelectStarExpression) throw Unsupported(element);
            if (element is not SelectScalarExpression selected || selected.Expression is not ColumnReferenceExpression reference) throw Unsupported(element);
            var identifiers = reference.MultiPartIdentifier?.Identifiers;
            if (identifiers is null || identifiers.Count is < 1 or > 2
                || (identifiers.Count == 2 && identifiers[0].Value != (source.Alias?.Value ?? table.Name))) throw Unsupported(element);
            var column = Column(table, identifiers[^1].Value);
            var columnName = statement.Columns.Count > 0
                ? statement.Columns.ElementAtOrDefault(view.Columns.Count)?.Value ?? throw Unsupported(statement)
                : selected.ColumnName?.Identifier?.Value ?? column.Name;
            view.Columns.Add(column with { Ordinal = view.Columns.Count + 1, Name = columnName, HasDefault = false, Sparse = false, Hidden = false, Generated = "NOT_APPLICABLE" });
        }
        Require((statement.Columns.Count == 0 || statement.Columns.Count == view.Columns.Count) && view.Columns.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count() == view.Columns.Count,
            statement, "View column names must be unique and match the SELECT list.");
        if (old is not null) model.Relations.Remove(old);
        model.Relations.Add(view);
    }

    private void Trigger(TriggerStatementBody statement)
    {
        if (statement.TriggerObject.TriggerScope != TriggerScope.Normal || statement.TriggerObject.Name is null || statement.WithAppend || statement.MethodSpecifier is not null) throw Unsupported(statement);
        var table = Relation(statement.TriggerObject.Name);
        var (schema, name) = Name(statement.Name);
        Require(schema == table.Schema, statement, "The trigger schema must match its table or view.");
        var old = table.Triggers.SingleOrDefault(t => t.Name == name);
        CheckCreateAlter(statement, old is not null);
        if (old is not null) table.Triggers.Remove(old);
        table.Triggers.Add(new(name, false, statement.TriggerType == TriggerType.InsteadOf));
    }

    private bool Evaluate(BooleanExpression expression)
    {
        if (expression is BooleanParenthesisExpression parentheses) return Evaluate(parentheses.Expression);
        if (expression is BooleanNotExpression not) return !Evaluate(not.Expression);
        if (expression is BooleanIsNullExpression test && test.Expression is FunctionCall call && call.CallTarget is null
            && call.FunctionName.Value.Equals("OBJECT_ID", StringComparison.OrdinalIgnoreCase)
            && call.Parameters.Count is 1 or 2 && call.Parameters[0] is StringLiteral text)
        {
            var name = new TSql180Parser(true).ParseSchemaObjectName(new StringReader(text.Value), out var errors);
            if (errors.Count > 0) throw Unsupported(expression);
            var (schema, value) = Name(name);
            var exists = ObjectExists(schema, value);
            if (call.Parameters.Count == 2)
            {
                if (call.Parameters[1] is not StringLiteral kind) throw Unsupported(expression);
                exists = kind.Value.ToUpperInvariant() switch
                {
                    "U" => model.Relations.Any(r => r.Schema == schema && r.Name == value && r.Kind == "Table"),
                    "V" => model.Relations.Any(r => r.Schema == schema && r.Name == value && r.Kind == "View"),
                    "P" => model.Routines.Any(r => r.Schema == schema && r.Name == value && r.Kind == "SQL_STORED_PROCEDURE"),
                    _ => throw Unsupported(expression)
                };
            }
            return test.IsNot ? exists : !exists;
        }
        throw Error(expression, "This condition cannot be resolved from the bundle. Only constant OBJECT_ID(...) IS NULL / IS NOT NULL guards are supported.");
    }

    private (string Schema, string Name) Name(SchemaObjectName name)
    {
        if (name.ServerIdentifier is not null || (name.DatabaseIdentifier is not null && name.DatabaseIdentifier.Value != scriptDatabase)
            || name.BaseIdentifier.Value.StartsWith('#')) throw Error(name, "Cross-database, server-qualified and temporary objects are not supported offline.");
        return (name.SchemaIdentifier?.Value ?? "dbo", name.BaseIdentifier.Value);
    }
    private bool ObjectExists(string schema, string name) => model.Relations.Any(r => r.Schema == schema && r.Name == name) || model.Routines.Any(r => r.Schema == schema && r.Name == name);
    private Relation Relation(SchemaObjectName name)
    {
        var (schema, value) = Name(name);
        return model.Relations.SingleOrDefault(r => r.Schema == schema && r.Name == value)
            ?? throw Error(name, "The table or view is missing from the bundle. Use exact schema/object names and apply scripts in order.");
    }
    private Relation Table(SchemaObjectName name)
    {
        var table = Relation(name);
        Require(table.Kind == "Table", name, "This statement requires a table.");
        return table;
    }
    private static Column Column(Relation table, string name) => table.Columns.SingleOrDefault(c => c.Name == name)
        ?? throw new UsageException("A referenced column is missing from the bundle. Use exact names and apply scripts in order. The bundle was preserved.");
    private static string ColumnName(ColumnReferenceExpression column) => column.MultiPartIdentifier?.Identifiers.Count == 1
        ? column.MultiPartIdentifier.Identifiers[0].Value : throw Unsupported(column);
    private static string Type(DataTypeReference type)
    {
        if (type is XmlDataTypeReference) return "xml";
        if (type is UserDataTypeReference user)
        {
            if (user.Name.Identifiers.Count > 2) throw Unsupported(type);
            static string Quote(string value) => "[" + value.Replace("]", "]]", StringComparison.Ordinal) + "]";
            return Quote(user.Name.SchemaIdentifier?.Value ?? "dbo") + "." + Quote(user.Name.BaseIdentifier.Value);
        }
        if (type is not SqlDataTypeReference sql) throw Unsupported(type);
        var name = sql.SqlDataTypeOption.ToString().ToLowerInvariant();
        var parameters = sql.Parameters.Select(p => p.Value.ToLowerInvariant()).ToList();
        if (name is "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary")
            return name + "(" + (parameters.FirstOrDefault() ?? "1") + ")";
        if (name is "decimal" or "numeric") return name + "(" + (parameters.FirstOrDefault() ?? "18") + "," + (parameters.ElementAtOrDefault(1) ?? "0") + ")";
        if (name is "datetime2" or "datetimeoffset" or "time") return name + "(" + (parameters.FirstOrDefault() ?? "7") + ")";
        if (name == "float") return int.Parse(parameters.FirstOrDefault() ?? "53", System.Globalization.CultureInfo.InvariantCulture) <= 24 ? "real" : "float(53)";
        if (name == "rowversion") return "timestamp";
        if (name is "vector" or "none") throw Unsupported(type);
        return name;
    }
    private static string Action(DeleteUpdateAction action) => action switch
    {
        DeleteUpdateAction.Cascade => "CASCADE",
        DeleteUpdateAction.SetNull => "SET_NULL",
        DeleteUpdateAction.SetDefault => "SET_DEFAULT",
        _ => "NO_ACTION"
    };
    private static void CheckCreateAlter(TSqlStatement statement, bool exists)
    {
        var type = statement.GetType().Name;
        if (type.StartsWith("CreateOrAlter", StringComparison.Ordinal)) return;
        Require(type.StartsWith("Create", StringComparison.Ordinal) ? !exists : exists, statement, "CREATE/ALTER does not match the starting bundle's object state.");
    }
    private static void Require(bool valid, TSqlFragment fragment, string message)
    {
        if (!valid) throw Error(fragment, message);
    }
    private static void RequireUnpartitioned(FileGroupOrPartitionScheme? storage)
    {
        if (storage?.PartitionSchemeColumns.Count > 0) throw Unsupported(storage);
    }
    private static UsageException Unsupported(TSqlFragment fragment) => Error(fragment,
        $"Unsupported offline SQL construct ({fragment.GetType().Name}). Use supported declarative DDL or refresh this scope from the database after applying the script.");
    private static UsageException Error(TSqlFragment fragment, string message) => new($"SQL line {fragment.StartLine}: {message} The bundle was preserved.");
}
