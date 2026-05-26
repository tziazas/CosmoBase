# Changelog

All notable changes to CosmoBase are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
CosmoBase uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.1] — 2026-05-25

### Fixed

- **`GetAllByPropertyComparisonAsync` generated invalid SQL.** `BuildSqlWhereClause` returns WHERE-clause
  predicates only, but the repository was passing them directly to `QueryDefinition` without the
  `SELECT * FROM c WHERE` prefix. Every call with at least one filter failed with a Cosmos DB
  `BadRequest` syntax error. The full query is now composed correctly before execution.

### Added

- **35 new tests** — bringing the total from 49 to 147:
  - **Unit — `AuditFieldManagerTests` (14 tests):** covers `SetCreateAuditFields`,
    `SetUpdateAuditFields`, `SetUpsertAuditFields`, and `SetBulkAuditFields` — including
    timestamp equality, field preservation on update, `DateTime.MinValue` treated as new-document,
    backfill of `CreatedOnUtc` when absent, null user-context guard, and `IUserContext` call
    verification via mock.
  - **Integration — `SoftDeleteIntegrationTests` (7 tests):** soft-deleted documents are hidden
    from `GetAllAsync` and `GetCountAsync` but visible via `GetByIdAsync(includeDeleted: true)`;
    audit fields (`Deleted`, `UpdatedOnUtc`, `UpdatedBy`) are set correctly; `GetTotalCountAsync`
    includes soft-deleted items while `GetCountAsync` excludes them; hard delete removes the
    document permanently from both count queries.
  - **Integration — `QueryIntegrationTests` (13 tests):** `SqlSpecification` with parameterized
    queries, `ORDER BY`, and multi-condition `WHERE`; `GetAllByPropertyComparisonAsync` with
    `Equal`, `GreaterThan`, and `includeDeleted`; scalar array containment via `QueryAsync` +
    `ARRAY_CONTAINS`; `GetPageWithTokenAsync` — first-page token present, last-page token null,
    consecutive pages non-overlapping; `GetPageWithTokenAndCountAsync` — total count on first
    page only, null on subsequent pages; `BulkReadAsyncEnumerable` batch streaming.
  - **Integration — `PatchIntegrationTests` (8 tests):** `Replace` updates a single field;
    multiple `Replace` operations in one call; `Add` sets a null nullable field; `Remove` clears
    a field; `Set` and `Increment` throw `CosmoBaseException` (wrapping `NotSupportedException`);
    `UpsertAsync` sets `CreatedOnUtc`/`CreatedBy` for new documents and preserves them for
    existing ones.

---

## [1.0.0] — 2026-05-24

First production-ready release. This version is a comprehensive quality pass over the 0.1.x
series, addressing safety issues, API consistency, correctness bugs, and dead code.

### Breaking Changes

- **`ICosmosDataReadService` and `ICosmosDataWriteService` no longer inherit from
  `IDataReadService<TDto, string>` and `IDataWriteService<TDto, string>`.** The inherited
  single-key overloads (`GetByIdAsync(id)`, `DeleteAsync(id)`) were impossible to implement
  correctly — Cosmos DB requires a partition key for point reads and deletes. All three explicit
  implementations were throwing `NotImplementedException` or `CosmoBaseException`. The broken
  inheritance is removed; use the partition-key overloads on the Cosmos-specific interfaces.

- **`GetAllAsync(int, int, int)` parameter rename:**
  - `limit` → `pageSize` — clarifies it is the server-side `LIMIT` per SDK round-trip, not the
    total number of results.
  - `count` → `maxItems` — clarifies it is the in-process yield cap across all pages.

### Fixed

- **SQL injection in array-property queries.** The `GetAllByArrayPropertyAsync` path and the
  inline query builder used string interpolation with caller-supplied identifiers. All dynamic
  identifier names are now validated against `CosmosValidationConstants.SafePropertyNamePattern`
  before being interpolated into SQL.

- **`SoftDeleteAsync` had no concurrency guard.** The method read the document with
  `GetItemAsync` (which discards the `ItemResponse` and its ETag), set `Deleted = true`, then
  replaced unconditionally. A concurrent write between the two steps was silently overwritten.
  The method now reads via `ReadItemAsync`, captures the ETag, and passes it as an `IfMatchEtag`
  precondition on the replace — causing a `412 Precondition Failed` on conflict instead of a
  silent data loss.

- **`ConvertToCountQuery` produced broken SQL for non-trivial projections.** The regex only
  matched `SELECT *` and left `ORDER BY` and `OFFSET/LIMIT` clauses in place, which are invalid
  in a `COUNT` query context. The converter now handles all `SELECT` projections and strips
  pagination and ordering clauses before executing the count.

- **`GetAllAsync()` (no partition key) issued a cross-partition fan-out with no signal.** The
  method set `allowCrossPartitionQuery = true` silently. It now logs a `LogWarning` on every call
  and the XML doc carries an `⚠ Cross-partition fan-out` callout.

### Changed

- **Partition key access replaced reflection with compiled expression trees.** `GetProperty()` /
  `GetValue()` were called on every `CreateAsync`, `ReplaceAsync`, and `UpsertAsync`. The
  selector is now compiled once per `(T, partitionKeyProperty)` pair and cached, eliminating
  per-write reflection overhead under write throughput.

- **`GetCountWithCacheAsync` dual-expiry system removed.** The method maintained a
  `CachedCountEntry` wrapper with a manual `CachedAt` timestamp AND simultaneously set a 24-hour
  `AbsoluteExpiration` on the same cache entry — two independent expiry systems. The wrapper is
  gone; `IMemoryCache` handles expiry exclusively via the caller-supplied `cacheExpiryMinutes`
  parameter.

- **`QueryAsync` and `BulkReadAsyncEnumerable` throw `NotSupportedException`** (instead of
  `ArgumentException`) when a non-`SqlSpecification<T>` is passed. The exception type now
  correctly reflects an unsupported operation rather than a bad argument.

### Removed

- **Polly dependency removed.** Retry logic is now handled entirely by the Cosmos DB SDK's
  built-in retry policy (`MaxRetryAttemptsOnRateLimitedRequests`,
  `MaxRetryWaitTimeOnRateLimitedRequests`). Adding a Polly layer on top doubled retry delays and
  obscured the SDK's own diagnostic telemetry.

- **Newtonsoft.Json dependency removed** from `CosmoBase.Core`. No API from the package was
  called; the two `using Newtonsoft.Json;` directives were unused.

- **Spurious `new()` constraint removed** from `CosmosRepository<T>`, `ICosmosValidator<T>`, and
  `CosmosValidator<T>`. The constraint was absent from counterpart interfaces and unused in any
  method body, but it silently prevented types without a public parameterless constructor from
  using the implementations.

- **Abandoned `_disposed` field removed** from `CosmosRepository`. The field was never read or
  written. `CosmosRepository` does not own its `CosmosClient` instances (DI-managed singletons),
  and `Container` is not `IDisposable`, so the class has no disposal obligations.

- **Debug-only test methods removed.** Several `Debug_*` methods in the integration test suite
  had no assertions and used `output.WriteLine` to report results. They were written to diagnose
  a partition-key reflection bug that has since been fixed; the real integration tests cover all
  paths they probed.

### Infrastructure

- **CI updated to Node.js 24.** Added `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24: 'true'` environment
  variable and upgraded `actions/setup-dotnet` to `@v4` to resolve Node.js 20 deprecation
  warnings in the GitHub Actions publish pipeline.

- **Integration tests no longer require cloud credentials.** The test fixture detects whether a
  `localsettings.json` override is present. When absent (GitHub Actions, fresh clone), it spins
  up a Cosmos DB emulator via `Testcontainers.CosmosDb` in Docker and overrides the DI-registered
  `CosmosClient` to accept the emulator's self-signed TLS certificate.

- **README rewritten** to accurately reflect the current API: correct parameter names, all query
  overloads with cross-partition warnings, `SqlSpecification<T>` with parameterized examples,
  `PropertyFilter` with comparison operators, patch operations, bulk operations, metrics table,
  and a complete configuration reference.

---

## [0.1.4] — 2025

- Removed manual disposing of repository (`CosmosRepository` no longer implements `IDisposable`).

## [0.1.3] — 2025

- Minor patches and README updates.

## [0.1.0] — 2025

Initial public release.

- Azure Cosmos DB repository pattern with DAO/DTO separation.
- `IAsyncEnumerable<T>` streaming for large result sets.
- `SqlSpecification<T>` parameterized query support.
- `PropertyFilter` / `GetAllByPropertyComparisonAsync` with comparison operators.
- Continuation-token pagination (`GetPageWithTokenAsync`, `GetPageWithTokenAndCountAsync`).
- Automatic audit field management (`CreatedOnUtc`, `UpdatedOnUtc`, `CreatedBy`, `UpdatedBy`,
  `Deleted`).
- Soft delete with configurable hard-delete option.
- Bulk insert and upsert with configurable batch size and concurrency.
- `IMemoryCache`-backed count caching.
- OpenTelemetry-compatible metrics via `System.Diagnostics.Metrics`.
- Named multi-region Cosmos client support.
- Three DI registration helpers: `AddCosmoBase`, `AddCosmoBaseWithSystemUser`,
  `AddCosmoBaseWithUserProvider`.
