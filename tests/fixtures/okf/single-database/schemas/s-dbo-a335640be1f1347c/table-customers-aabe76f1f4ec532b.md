---
type: "SQL Server Table"
title: "dbo.Customers"
description: "Table structure, indexes and declared relationships."
status: draft
generated:
  by: dbmapper/0.3.0
sources:
  - resource: "SQL Server schema metadata for this scope; credentials and server identity omitted"
---

# dbo.Customers

Kind: Table. Temporal role: <code>NON&#95;TEMPORAL&#95;TABLE</code>.

# Schema

| Ordinal | Column | SQL type | Nullable | Identity | Computed | Default present | Sparse | Hidden | Generated | Collation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | <code>TenantId</code> | <code>int</code> | no | no | no | no | no | no | <code>NOT&#95;APPLICABLE</code> | — |
| 2 | <code>Id</code> | <code>int</code> | no | no | no | no | no | no | <code>NOT&#95;APPLICABLE</code> | — |

# Keys and indexes

## PK&#95;Customers

Type: <code>CLUSTERED</code>; unique: yes; primary key: yes; unique constraint: no; filtered: no; disabled: no.

| Catalog ordinal | Column | Key ordinal | Direction | Explicit include | Partition ordinal |
| --- | --- | --- | --- | --- | --- |
| 1 | <code>TenantId</code> | 1 | ASC | no | — |
| 2 | <code>Id</code> | 2 | ASC | no | — |

# Foreign keys

No declared foreign keys were observed.
# Referenced by

* [sales.Orders](../s-sales-e04eb29020eaa961/table-orders-b7e8acdd522e6190.md) via <code>FK&#95;Orders&#95;Customers</code>.

# Check constraints

| Name | Disabled | Untrusted |
| --- | --- | --- |

Expressions are omitted. Empty lists mean none were observed.

# Triggers

| Name | Disabled | Instead of |
| --- | --- | --- |

Bodies and event logic are omitted.

[Schema index](index.md) · [Coverage and limitations](../../database.md)

<!-- dbmapper:sha256=0080969df36b8a98d9c2c5b95e2968018e4bf9f5bb1fc2789f21ec0bcf8c9ff3 -->
