-- Fixed catalog-only allowlist. Never add row queries, definitions, properties,
-- current sequence/identity values, SQL text, statistics or dynamic SQL here.
SET NOCOUNT ON;
SET LOCK_TIMEOUT 5000;
SET DEADLOCK_PRIORITY LOW;

SELECT CAST(COALESCE(HAS_PERMS_BY_NAME(NULL, 'DATABASE', 'VIEW DEFINITION'), 0) AS int);

SELECT o.object_id, s.name, o.name,
       CASE o.type WHEN 'U' THEN 'Table' ELSE 'View' END,
       COALESCE(t.temporal_type_desc, 'NOT_APPLICABLE')
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
LEFT JOIN sys.tables AS t ON t.object_id = o.object_id
WHERE o.type IN ('U', 'V') AND o.is_ms_shipped = 0
ORDER BY s.name, o.name;

SELECT c.object_id, c.column_id, c.name,
       CASE WHEN ty.is_user_defined = 1 THEN QUOTENAME(ts.name) + '.' + QUOTENAME(ty.name)
       ELSE ty.name + CASE
           WHEN ty.name IN ('varchar', 'char', 'varbinary', 'binary')
               THEN '(' + CASE WHEN c.max_length = -1 THEN 'max' ELSE CONVERT(varchar(10), c.max_length) END + ')'
           WHEN ty.name IN ('nvarchar', 'nchar')
               THEN '(' + CASE WHEN c.max_length = -1 THEN 'max' ELSE CONVERT(varchar(10), c.max_length / 2) END + ')'
           WHEN ty.name IN ('decimal', 'numeric')
               THEN '(' + CONVERT(varchar(10), c.precision) + ',' + CONVERT(varchar(10), c.scale) + ')'
           WHEN ty.name IN ('datetime2', 'datetimeoffset', 'time')
               THEN '(' + CONVERT(varchar(10), c.scale) + ')'
           WHEN ty.name = 'float' THEN '(' + CONVERT(varchar(10), c.precision) + ')'
           ELSE '' END END,
       c.is_nullable, c.is_identity, c.is_computed,
       CAST(CASE WHEN c.default_object_id <> 0 OR ty.default_object_id <> 0 THEN 1 ELSE 0 END AS bit),
       c.is_sparse, c.is_hidden, c.generated_always_type_desc, c.collation_name
FROM sys.columns AS c
JOIN sys.objects AS o ON o.object_id = c.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
JOIN sys.schemas AS ts ON ts.schema_id = ty.schema_id
WHERE o.type IN ('U', 'V') AND o.is_ms_shipped = 0
ORDER BY c.object_id, c.column_id;

SELECT i.object_id, i.index_id, i.name, i.type_desc, i.is_unique,
       i.is_primary_key, i.is_unique_constraint, i.has_filter, i.is_disabled
FROM sys.indexes AS i
JOIN sys.objects AS o ON o.object_id = i.object_id
WHERE o.type IN ('U', 'V') AND o.is_ms_shipped = 0
  AND i.index_id > 0 AND i.is_hypothetical = 0
ORDER BY i.object_id, i.index_id;

SELECT ic.object_id, ic.index_id, ic.index_column_id, c.name,
       ic.key_ordinal, ic.is_descending_key, ic.is_included_column, ic.partition_ordinal
FROM sys.index_columns AS ic
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
JOIN sys.objects AS o ON o.object_id = ic.object_id
JOIN sys.indexes AS i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
WHERE o.type IN ('U', 'V') AND o.is_ms_shipped = 0
  AND i.index_id > 0 AND i.is_hypothetical = 0
ORDER BY ic.object_id, ic.index_id, ic.index_column_id;

SELECT fk.parent_object_id, fk.object_id, fk.name, fk.referenced_object_id,
       rs.name, ro.name, fk.delete_referential_action_desc, fk.update_referential_action_desc,
       fk.is_disabled, fk.is_not_trusted, fkc.constraint_column_id, pc.name, rc.name
FROM sys.foreign_keys AS fk
JOIN sys.objects AS po ON po.object_id = fk.parent_object_id
JOIN sys.objects AS ro ON ro.object_id = fk.referenced_object_id
JOIN sys.schemas AS rs ON rs.schema_id = ro.schema_id
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns AS pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.columns AS rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
WHERE po.is_ms_shipped = 0
ORDER BY fk.parent_object_id, fk.object_id, fkc.constraint_column_id;

SELECT cc.parent_object_id, cc.name, cc.is_disabled, cc.is_not_trusted
FROM sys.check_constraints AS cc
JOIN sys.objects AS o ON o.object_id = cc.parent_object_id
WHERE o.type = 'U' AND o.is_ms_shipped = 0
ORDER BY cc.parent_object_id, cc.name;

SELECT tr.parent_id, tr.name, tr.is_disabled, tr.is_instead_of_trigger
FROM sys.triggers AS tr
JOIN sys.objects AS o ON o.object_id = tr.parent_id
WHERE tr.parent_class = 1 AND tr.is_ms_shipped = 0
  AND o.type IN ('U', 'V') AND o.is_ms_shipped = 0
ORDER BY tr.parent_id, tr.name;

SELECT o.object_id, s.name, o.name, o.type_desc
FROM sys.objects AS o
JOIN sys.schemas AS s ON s.schema_id = o.schema_id
WHERE o.type IN ('P', 'PC', 'FN', 'IF', 'TF', 'FS', 'FT') AND o.is_ms_shipped = 0
ORDER BY s.name, o.name;

SELECT p.object_id, p.parameter_id, p.name,
       CASE WHEN ty.is_user_defined = 1 THEN QUOTENAME(ts.name) + '.' + QUOTENAME(ty.name)
       ELSE ty.name + CASE
           WHEN ty.name IN ('varchar', 'char', 'varbinary', 'binary')
               THEN '(' + CASE WHEN p.max_length = -1 THEN 'max' ELSE CONVERT(varchar(10), p.max_length) END + ')'
           WHEN ty.name IN ('nvarchar', 'nchar')
               THEN '(' + CASE WHEN p.max_length = -1 THEN 'max' ELSE CONVERT(varchar(10), p.max_length / 2) END + ')'
           WHEN ty.name IN ('decimal', 'numeric')
               THEN '(' + CONVERT(varchar(10), p.precision) + ',' + CONVERT(varchar(10), p.scale) + ')'
           WHEN ty.name IN ('datetime2', 'datetimeoffset', 'time')
               THEN '(' + CONVERT(varchar(10), p.scale) + ')'
           WHEN ty.name = 'float' THEN '(' + CONVERT(varchar(10), p.precision) + ')'
           ELSE '' END END,
       p.is_output, p.is_readonly
FROM sys.parameters AS p
JOIN sys.objects AS o ON o.object_id = p.object_id
JOIN sys.types AS ty ON ty.user_type_id = p.user_type_id
JOIN sys.schemas AS ts ON ts.schema_id = ty.schema_id
WHERE o.type IN ('P', 'PC', 'FN', 'IF', 'TF', 'FS', 'FT') AND o.is_ms_shipped = 0
ORDER BY p.object_id, p.parameter_id;
