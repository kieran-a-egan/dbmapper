---
type: "SQL Server Database"
title: "Database structure"
description: "Scope and safety boundaries of this database context."
status: draft
generated:
  by: dbmapper/0.2.0
sources:
  - resource: "SQL Server catalog metadata for this scope; credentials and server identity omitted"
---

# Scope

This section contains 2 tables, 0 views and 0 routines from the database explicitly selected in a local .NET user secret. Database and server identities are intentionally absent.

# Included metadata

* Schemas containing supported objects; table and view column order, SQL types, nullability, collation and generation flags.
* Primary keys, unique constraints, index types, key order/direction, explicit included columns, partition ordinals and filter/disabled flags.
* Foreign key column pairs in declared order, update/delete actions and trust/disabled flags; check and trigger names/state.
* Procedure and function names, kinds and catalog parameter signatures. Scalar function parameter zero is the return value.

# Omitted information

No application rows, samples, row counts, statistics, identity/sequence current values, connection strings, hosts, database names, logins, permissions, file locations, comments or extended properties are collected into this section. Default/computed/check/filter expressions, view/routine/trigger bodies and parameter default values are never selected, even when they appear harmless. A presence flag does not describe an expression's behavior.

This is development context, not a DDL backup or migration script. Empty schemas, sequences, synonyms, user-defined type definitions, table-valued function result columns, full-text configuration, partition boundaries, security policies and detailed specialist index options are outside this version's coverage. Alias/CLR/table type names can appear without their definitions. Catalog-implicit index columns are not invented.

# Interpretation and freshness

Only declared relationships are recorded. Names do not establish business rules, tenant boundaries, cardinality or authorization. Missing metadata is not evidence that an object or rule is absent. SQL Server permissions, object-level restrictions and concurrent DDL can limit visibility; database VIEW DEFINITION is checked, but this is not a transactionally consistent schema snapshot. Run while schema changes are idle and refresh after migrations.

Output is deterministic: identical metadata yields identical bytes. Timestamps are omitted; use repository history and rerun the tool to check freshness. No human review or independent verification is claimed. Identifiers are retained because code needs them; identifiers can themselves be confidential and must be reviewed before committing or publishing.

# Navigation

Start with the [bundle index](index.md) and [agent guide](CLAUDE.md).

<!-- dbmapper:sha256=5f889d9272a603e1349a6ea26bef5b1bb09b1b753be2e24b13d42c776297bad2 -->
