namespace DbMapper;

internal sealed class SchemaModel
{
    public List<Relation> Relations { get; init; } = [];
    public List<Routine> Routines { get; init; } = [];
}

internal sealed record Relation(int Id, string Schema, string Name, string Kind, string Temporal)
{
    public List<Column> Columns { get; init; } = [];
    public List<DbIndex> Indexes { get; init; } = [];
    public List<ForeignKey> ForeignKeys { get; init; } = [];
    public List<CheckConstraint> Checks { get; init; } = [];
    public List<Trigger> Triggers { get; init; } = [];
}

internal sealed record Column(int Ordinal, string Name, string Type, bool Nullable, bool Identity, bool Computed,
    bool HasDefault, bool Sparse, bool Hidden, string Generated, string? Collation);

internal sealed record DbIndex(int Id, string Name, string Kind, bool Unique, bool PrimaryKey, bool UniqueConstraint, bool Filtered, bool Disabled)
{
    public List<IndexColumn> Columns { get; init; } = [];
}
internal sealed record IndexColumn(int Ordinal, string Name, int KeyOrdinal, bool Descending, bool Included, int PartitionOrdinal);
internal sealed record ForeignKey(int Id, string Name, int TargetId, string TargetSchema, string TargetName, string OnDelete, string OnUpdate, bool Disabled, bool Untrusted)
{
    public List<KeyPair> Columns { get; init; } = [];
}
internal sealed record KeyPair(int Ordinal, string Source, string Target);
internal sealed record CheckConstraint(string Name, bool Disabled, bool Untrusted);
internal sealed record Trigger(string Name, bool Disabled, bool InsteadOf);
internal sealed record Routine(int Id, string Schema, string Name, string Kind)
{
    public List<Parameter> Parameters { get; init; } = [];
}
internal sealed record Parameter(int Ordinal, string Name, string Type, bool Output, bool ReadOnly);
