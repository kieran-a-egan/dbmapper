---
type: "SQL Server Table"
title: "sales.Orders"
description: "Table structure, indexes and declared relationships."
status: draft
generated:
  by: dbmapper/0.2.0
sources:
  - resource: "SQL Server catalog metadata for this scope; credentials and server identity omitted"
---

# sales.Orders

Kind: Table. Temporal role: <code>NON&#95;TEMPORAL&#95;TABLE</code>.

# Schema

| Ordinal | Column | SQL type | Nullable | Identity | Computed | Default present | Sparse | Hidden | Generated | Collation |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | <code>TenantId</code> | <code>int</code> | no | no | no | no | no | no | <code>NOT&#95;APPLICABLE</code> | — |
| 2 | <code>CustomerId</code> | <code>int</code> | no | no | no | no | no | no | <code>NOT&#95;APPLICABLE</code> | — |
| 3 | <code>Amount</code> | <code>decimal&#40;18&#44;2&#41;</code> | no | no | no | no | no | no | <code>NOT&#95;APPLICABLE</code> | — |

# Keys and indexes

## IX&#95;Orders&#95;Customer

Type: <code>NONCLUSTERED</code>; unique: no; primary key: no; unique constraint: no; filtered: no; disabled: no.

| Catalog ordinal | Column | Key ordinal | Direction | Explicit include | Partition ordinal |
| --- | --- | --- | --- | --- | --- |
| 1 | <code>TenantId</code> | 1 | ASC | no | — |
| 2 | <code>CustomerId</code> | 2 | DESC | no | — |
| 3 | <code>Amount</code> | — | — | yes | — |

# Foreign keys

## FK&#95;Orders&#95;Customers

References [dbo.Customers](../s-dbo-a335640be1f1347c/table-customers-aabe76f1f4ec532b.md). On delete: <code>NO&#95;ACTION</code>; on update: <code>NO&#95;ACTION</code>; disabled: no; untrusted: no.

| Position | Local column | Referenced column |
| --- | --- | --- |
| 1 | <code>TenantId</code> | <code>TenantId</code> |
| 2 | <code>CustomerId</code> | <code>Id</code> |

# Referenced by

No incoming declared foreign keys were observed.

# Check constraints

| Name | Disabled | Untrusted |
| --- | --- | --- |

Expressions are omitted. Empty lists mean none were observed.

# Triggers

| Name | Disabled | Instead of |
| --- | --- | --- |

Bodies and event logic are omitted.

[Schema index](index.md) · [Coverage and limitations](../../database.md)

<!-- dbmapper:sha256=2427ba9a0cb644e1972defe68cf4f483c07c4cb8aa1a0f32eedaab6b2f446432 -->
