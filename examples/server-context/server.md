---
type: "SQL Server Scan"
title: "Server database scan"
description: "Per-database export status and scope limitations."
status: draft
generated:
  by: dbmapper/0.2.0
sources:
  - resource: "SQL Server catalog metadata for this scope; credentials and server identity omitted"
---

# Scan result

Incomplete: some discovered databases were not exported. Exported 2 of 3 discovered databases.

# Scope

Discovery reads sys.databases in master using the same server, authentication and TLS settings as the selected user secret. Each visible online and accessible database is inspected sequentially with the existing fixed metadata-only queries. User databases, system databases and visible snapshots are included. No database is brought online or altered, and no application data or stored code is read/executed. Database names are intentionally included as structural identifiers; server addresses, credentials and raw error messages remain excluded.

# Coverage limitations

Only databases visible to this login can be listed. VIEW ANY DATABASE is required, but SQL Server can still hide offline databases from low-privilege logins. Discovery and individual scans are not a server-wide consistent snapshot: databases may be created, dropped, renamed or become unavailable during the run. Exit 0 means every discovered database was exported, not that hidden databases cannot exist.

Each successful database still requires VIEW DEFINITION and follows its own database.md coverage limitations. Microsoft-shipped objects are filtered out, including inside system databases. No cross-database dependencies or relationships are inferred. Review database names along with all other identifiers before committing.

# Incomplete exports

Skipped or failed databases receive a status page with no schema files. An incomplete run writes the accessible results and returns exit code 4. Refresh replaces the previous generated snapshot, including removal of older pages for databases that could not be scanned this time. Do not interpret those missing pages as schema deletions. Discovery failure, cancellation and unexpected application failure leave the existing bundle unchanged.

# Databases

| Database | Kind | Status |
| --- | --- | --- |
| [Application](databases/db-application-e7ad522ea327e5ba/index.md) | User / snapshot | Exported. |
| [Archive](databases/db-archive-66f4804ee23ddc09/index.md) | User / snapshot | Skipped&#58; database is not online. |
| [Reporting](databases/db-reporting-b1fa104b9bf0635e/index.md) | User / snapshot | Exported. |

<!-- dbmapper:sha256=521b9cff03033131db4bab0c1706b2838d08020635972e584285fcc27d0aad4a -->
