# CosmoBase Code Review

> Reviewed: 2026-05-24  
> Branch: `release/1.0.0`

## Changelog

| Date | Finding | What changed |
|------|---------|--------------|
| 2026-05-24 | #1 — SQL injection (`IN` clause) | `PropertyFilterExtensions.BuildSqlWhereClause` now emits `@{col}_{filterIdx}_in_{valueIdx}` parameters for `IN` filters instead of inlining string literals. `AddParameters` updated to bind each value. 17 unit tests added at `tests/CosmoBase.Tests/Unit/Extensions/PropertyFilterExtensionsTests.cs`. |
| 2026-05-24 | #2 — SQL injection (array query identifiers) | `CosmosValidationConstants` now exposes a compiled `SafePropertyNamePattern` regex (`^[a-zA-Z_][a-zA-Z0-9_.]*$`). `CosmosValidator.ValidateArrayPropertyQuery` validates both `arrayName` and `elementPropertyName` against this pattern before they are interpolated into SQL, rejecting anything containing spaces, quotes, semicolons, or other non-identifier characters. 35 unit tests added at `tests/CosmoBase.Tests/Unit/Validators/CosmosValidatorArrayQueryTests.cs`. |
| 2026-05-24 | #3 — Polly registered but never used | Removed `TryAddSingleton` Polly registration and `using Polly;` from `ServiceCollectionExtensions.cs`. Removed `<PackageReference Include="Polly" />` from `CosmoBase.Core.csproj` and stale release note entry. 10 DI registration unit tests added at `tests/CosmoBase.Tests/Unit/DependencyInjection/ServiceRegistrationTests.cs`. |
| 2026-05-24 | #4 — CI pipeline had no test step | Restructured `.github/workflows/publish.yml` into two jobs: `test` (unit + integration, uploads `.trx` results) and `publish` (`needs: test`). Replaced verbose per-project restore/build with `dotnet build CosmoBase.sln`. |
| 2026-05-24 | #5 — `NotImplementedException` from explicit interface implementations | Removed `IDataWriteService<TDto, string>` and `IDataReadService<TDto, string>` from the inheritance chains of `ICosmosDataWriteService` and `ICosmosDataReadService`. Removed the three explicit throwing implementations (`IDataWriteService.CreateAsync`, `IDataWriteService.DeleteAsync`, `IDataReadService.GetByIdAsync`). Removed `new` modifier from `CreateAsync`, `UpsertAsync`, `GetAllAsync`, and `QueryAsync` in the Cosmos interfaces. Removed `IDataReadService<,>` and `IDataWriteService<,>` open-generic DI registrations from `ServiceCollectionExtensions.cs`. |
| 2026-05-24 | #6 — Reflection on every write (not cached) | Replaced `typeof(T).GetProperty(_partitionKeyProperty).GetValue(item)` in `GetPartitionKeyValue` with a compiled `Func<T, string>` delegate (`_getPartitionKey`) built once in the constructor via `Expression.Lambda<Func<T,string>>(...).Compile()`. Added `BuildPartitionKeyAccessor` static helper with step-by-step XML doc explaining the expression tree. Property existence is now validated at construction time rather than on the first write. |
| 2026-05-24 | #7 — Dead Newtonsoft.Json dependency | Removed `<PackageReference Include="Newtonsoft.Json" />` and the redundant commented-out duplicate from `CosmoBase.Core.csproj`. Removed `using Newtonsoft.Json;` from `CosmosRepository.cs` and `CosmosDataWriteService.cs`. Added `<AzureCosmosDisableNewtonsoftJsonCheck>true</AzureCosmosDisableNewtonsoftJsonCheck>` to the root `Directory.Build.props` (with explanation) so the Cosmos SDK v3 build guard does not require Newtonsoft across any project in the solution. |
| 2026-05-24 | #8 — `_disposed` dead code | Deleted the `private bool _disposed` field from `CosmosRepository<T>`. The repository does not own its `CosmosClient` instances (they are shared singletons injected via DI) and `Container` is not `IDisposable`, so there are no resources to release and implementing `IDisposable` would be incorrect. |
| 2026-05-24 | #10 — Manual cache expiry duplicates `IMemoryCache` | Removed `CachedCountEntry` wrapper class and the manual `DateTime.UtcNow - cachedEntry.CachedAt` age check from `GetCountWithCacheAsync`. The cache now stores `int` directly. `GetFreshCountAsync` receives `cacheExpiryMinutes` and sets `AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheExpiryMinutes)` so `IMemoryCache` owns the TTL. The `cacheExpiryMinutes == 0` bypass now correctly calls `GetCountAsync` directly without touching the cache. Deleted `CachedCountEntry.cs` from `CosmoBase.Abstractions`. |
| 2026-05-24 | #11 — Debug tests in integration test suite | Deleted all 20 `Debug_*` test methods from `DataServicesIntegrationTests.cs`. All were written to diagnose a partition-key reflection bug that is now fixed; none had assertions. The real integration tests (14 `[Fact]`/`[Theory]` methods starting at the bottom of the file) already cover every code path those debug tests were probing. Also removed the 6 `using` directives that existed solely to support the debug tests (`System.Reflection`, `Microsoft.Azure.Cosmos`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.Options`, `CosmoBase.Abstractions.Configuration`, and the `IConfiguration` Castle alias). |
| 2026-05-24 | #12 — Soft delete has no optimistic concurrency | Rewrote `SoftDeleteAsync` to call `_readContainer.ReadItemAsync<T>` directly instead of `GetItemAsync`, capturing the `ItemResponse<T>` and its `ETag`. The subsequent `ReplaceItemAsync` now passes `new ItemRequestOptions { IfMatchEtag = readResponse.ETag }`, causing Cosmos to return HTTP 412 PreconditionFailed if the document was modified between the read and the write rather than silently overwriting concurrent changes. NotFound on read returns early (nothing to delete). |
| 2026-05-24 | #9 — `new()` constraint not on interface | Removed `new()` from `CosmosRepository<T>`, `ICosmosValidator<in T>`, and `CosmosValidator<T>`. The constraint was never used in any of their bodies and was absent from `ICosmosRepository<T>` and `IAuditFieldManager<T>`, silently narrowing which types could use the concrete implementations. |

---

## What's genuinely good

**Architecture and separation of concerns.**
The four-package layout (Abstractions → Core → DataServices → DI) is clean. The zero-dependency Abstractions package is the right call for a library — consumers can depend only on the interfaces without pulling in the entire stack.

**Fail-fast configuration validation.**
`CosmosConfigurationValidator` + `ValidateOnStart()` is exactly the right pattern. Loud startup failures rather than silent runtime errors.

**Integration tests use Testcontainers (real emulator).**
Mocking CosmosClient at the SDK level is nearly useless. Using a real emulator is the correct call.

**Built-in OpenTelemetry-compatible metrics.**
`System.Diagnostics.Metrics` for RU tracking is forward-looking and the right approach for a library.

**Structured logging throughout.**
Every operation logs the request charge and diagnostics. Whoever maintains this will thank you.

**Soft delete as a first-class concept.**
Automatic filtering, cache invalidation on deletes, and audit field management are all handled consistently.

**Named CosmosClient dictionary.**
Supporting separate read and write clients per model type is a real production need and is wired up cleanly.

---

## Serious — fix these

### ~~1. SQL injection in `GetAllByPropertyComparisonAsync`~~ ✅ Fixed
**File:** `src/CosmoBase.Core/Extensions/PropertyFilterExtensions.cs`

~~The `IN` comparison inlines values directly into the query string without parameterization~~

**Fix:** `IN` values are now emitted as numbered parameters (`@{col}_{filterIdx}_in_{valueIdx}`) in `BuildSqlWhereClause` and bound in `AddParameters`. Using the filter index prevents name collisions when the same column appears in multiple `IN` filters. Unit tests added at `tests/CosmoBase.Tests/Unit/Extensions/PropertyFilterExtensionsTests.cs`.

---

### ~~2. SQL injection in `GetAllByArrayPropertyAsync`~~ ✅ Fixed
**File:** `src/CosmoBase.Core/Repositories/CosmosRepository.cs` — `GetAllByArrayPropertyAsync`

~~Both `arrayName` and `elementPropertyName` are interpolated directly into the SQL query. Only the value is parameterized.~~

**Fix:** Since Cosmos SQL does not support parameterizing identifiers, the fix is validation before interpolation. `CosmosValidationConstants.SafePropertyNamePattern` (`^[a-zA-Z_][a-zA-Z0-9_.]*$`) is checked against both names in `CosmosValidator.ValidateArrayPropertyQuery` — which is already called at the top of `GetAllByArrayPropertyAsync` — so any name containing spaces, quotes, semicolons, or other non-identifier characters throws `ArgumentException` before the query is built. Unit tests added at `tests/CosmoBase.Tests/Unit/Validators/CosmosValidatorArrayQueryTests.cs`.

---

### ~~3. Polly is registered but never used~~ ✅ Fixed
**File:** `src/CosmoBase.DependencyInjection/ServiceCollectionExtensions.cs`

~~A Polly retry policy is registered as a singleton in `AddCosmoBaseInternal` but `CosmosRepository` never injects or calls it.~~

**Fix:** Removed the `TryAddSingleton` Polly registration and `using Polly;` from `ServiceCollectionExtensions.cs`. Removed `<PackageReference Include="Polly" />` from `CosmoBase.Core.csproj` and the stale "Retry policies and resilience patterns" release note entry. Rate-limit retries are handled by the Cosmos SDK itself via `MaxRetryAttemptsOnRateLimitedRequests`. 10 DI registration unit tests added at `tests/CosmoBase.Tests/Unit/DependencyInjection/ServiceRegistrationTests.cs`, including one that explicitly asserts no Polly descriptor is present in the container.

---

### ~~4. CI pipeline never runs tests~~ ✅ Fixed
**File:** `.github/workflows/publish.yml`

~~The workflow chain is: restore → build → pack → publish. There is no `dotnet test` step.~~

**Fix:** Restructured into two jobs. `test` runs first: restores and builds the full solution (`CosmoBase.sln`), then runs unit tests (`FullyQualifiedName~.Unit.`) and integration tests (`FullyQualifiedName~.Integration.`) as separate steps with `.trx` results uploaded as an artifact. `publish` has `needs: test`, so a failing test blocks the tag from reaching NuGet. The verbose per-project restore/build was also replaced with a single solution-level `dotnet build CosmoBase.sln`.

---

### ~~5. `NotImplementedException` thrown from explicit interface implementations~~ ✅ Fixed
**Files:**
- `src/CosmoBase.Abstractions/Interfaces/ICosmosDataWriteService.cs`
- `src/CosmoBase.Abstractions/Interfaces/ICosmosDataReadService.cs`
- `src/CosmoBase.DataServices/CosmosDataWriteService.cs`
- `src/CosmoBase.DataServices/CosmosDataReadService.cs`
- `src/CosmoBase.DependencyInjection/ServiceCollectionExtensions.cs`

~~`CosmosDataWriteService` and `CosmosDataReadService` explicitly implement base interface methods (`IDataWriteService<TDto,string>.CreateAsync`, `DeleteAsync`, `IDataReadService<TDto,string>.GetByIdAsync`) by throwing exceptions at runtime. If the base interface contract can never be correctly fulfilled, the inheritance hierarchy is wrong and those base interfaces should not be in the hierarchy.~~

**Fix:** Removed `: IDataWriteService<TDto, string>` and `: IDataReadService<TDto, string>` from the two Cosmos interface declarations. Removed the `new` modifier from the re-declared methods (`CreateAsync`, `UpsertAsync`, `GetAllAsync`, `QueryAsync`). Removed the three explicit throwing implementations from the service classes. Removed the `IDataReadService<,>` and `IDataWriteService<,>` open-generic DI registrations — consumers should always resolve the Cosmos-specific interfaces directly.

---

### ~~6. Reflection on every write operation~~ ✅ Fixed
**File:** `src/CosmoBase.Core/Repositories/CosmosRepository.cs:894`

`GetPartitionKeyValue` is called on every `CreateItemAsync`, `ReplaceItemAsync`, and `UpsertItemAsync`:

```csharp
private string GetPartitionKeyValue(T item)
{
    var prop = typeof(T).GetProperty(_partitionKeyProperty); // reflection every call
    ...
}
```

The `PropertyInfo` is resolved on every invocation. It should be cached at construction time (e.g., `Lazy<PropertyInfo>` field or a compiled `Func<T, string>`).

---

## Medium — should be improved

### ~~7. Dead Newtonsoft.Json dependency~~ ✅ Fixed
**Files:**
- `src/CosmoBase.Core/CosmoBase.Core.csproj:40`
- `src/CosmoBase.Core/Repositories/CosmosRepository.cs:15`
- `src/CosmoBase.DataServices/CosmosDataWriteService.cs:7`

`CosmoBase.Core.csproj` references `Newtonsoft.Json` as a package. The `using Newtonsoft.Json;` statements in `CosmosRepository.cs` and `CosmosDataWriteService.cs` are unused — no Newtonsoft API is called anywhere. There is even a commented-out reference in the `.csproj` with the note "Remove Newtonsoft.Json unless specifically needed." The package and using directives should be removed.

---

### ~~8. `_disposed` field with no `IDisposable` implementation~~ ✅ Fixed
**File:** `src/CosmoBase.Core/Repositories/CosmosRepository.cs:35`

```csharp
private bool _disposed;
```

This field is never read, never written, and the class does not implement `IDisposable`. It is dead code that implies an incomplete dispose pattern.

---

### ~~9. `new()` constraint mismatch between interface and implementation~~ ✅ Fixed
**File:** `src/CosmoBase.Core/Repositories/CosmosRepository.cs:24`

```csharp
// Interface
public interface ICosmosRepository<T> where T : class, ICosmosDataModel

// Implementation — adds new() not required by the interface
public class CosmosRepository<T> : ICosmosRepository<T> where T : class, ICosmosDataModel, new()
```

The `new()` constraint is not required by the interface and does not appear to be used anywhere inside `CosmosRepository`. It silently prevents types without a parameterless constructor from using the concrete repository, even though the interface contract would allow it.

---

### ~~10. Manual cache expiry duplicates what `IMemoryCache` already does~~ ✅ Fixed
**File:** `src/CosmoBase.Core/Repositories/CosmosRepository.cs:479–516`

`GetFreshCountAsync` stores a `CachedCountEntry` with a `CachedAt` timestamp. `GetCountWithCacheAsync` then manually computes the age and compares it against `cacheExpiryMinutes`. Simultaneously, the underlying cache entry is set with `AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)`.

Two parallel expiry systems are running. Setting `AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheExpiryMinutes)` on the entry directly would make the manual age-check logic and the `CachedCountEntry` wrapper unnecessary.

---

### ~~11. Debug tests left in the integration test suite~~ ✅ Fixed
**File:** `tests/CosmoBase.Tests/Integration/Services/DataServicesIntegrationTests.cs`

`Debug_Container_Partition_Key` and `Debug_Partition_Key_Issue` are debugging artifacts. Both use `output.WriteLine` for ad-hoc inspection and have no meaningful assertions. They should be converted to real tests or removed.

---

### ~~12. Soft delete has no optimistic concurrency~~ ✅ Fixed
**File:** `src/CosmoBase.Core/Repositories/CosmosRepository.cs:254`

`SoftDeleteAsync` is a read-then-write:
1. `GetItemAsync` — reads the document
2. Sets `Deleted = true`
3. `ReplaceItemAsync` — writes back without an ETag

A concurrent write between steps 1 and 3 will be silently overwritten. An ETag-based conditional replace (`ItemRequestOptions.IfMatchEtag`) is the correct pattern.

---

### 13. Regex-based count query conversion is fragile
**File:** `src/CosmoBase.Core/Extensions/SqlQueryExtensions.cs:47`

```csharp
Regex.Replace(originalQueryText, @"^\s*SELECT\s+\*\s+FROM", "SELECT VALUE COUNT(1) FROM", ...)
```

This works for simple `SELECT * FROM c WHERE ...` queries but fails silently for queries with JOINs, `SELECT c.field`, subqueries, or other non-trivial shapes. A safer approach is for callers to supply a separate count query, or for `SqlSpecification` to carry an optional `CountQueryText`.

---

## Minor / cosmetic

### 14. `GetAllAsync(int limit, int offset, int count)` naming
**File:** `src/CosmoBase.Core/Repositories/CosmosRepository.cs:286`

The three parameters (`limit`, `offset`, `count`) are confusing. `limit` is the server-side page size, `offset` is the skip count (both sent in the SQL), and `count` is an in-process early-exit cap passed to `ExecuteIterator`. Having `limit` and `count` with different semantics warrants either clearer names or a redesign of the method signature.

---

### 15. Cross-partition fan-out `GetAllAsync()` has no warning
**File:** `src/CosmoBase.Core/Repositories/CosmosRepository.cs:267`

`GetAllAsync()` with no arguments uses `Queryable.Where(x => !x.Deleted).ToFeedIterator()` on a `GetItemLinqQueryable(true)` (cross-partition). This is an expensive fan-out query with no documentation warning. At minimum the XML doc should note the cost.

---

### 16. `QueryAsync` and `BulkReadAsyncEnumerable` accept `ISpecification<T>` but only work with `SqlSpecification<T>`
**Files:**
- `src/CosmoBase.DataServices/CosmosDataReadService.cs:200`, `:245`

Both methods cast `ISpecification<TDto>` to `SqlSpecification<TDto>` internally and throw `ArgumentException` for anything else. The constraint should be visible at the call site — the method signatures should accept `SqlSpecification<T>` directly.

---

## Priority order

| # | Finding | Category | Status |
|---|---------|----------|--------|
| 1 | SQL injection — `IN` clause values not parameterized | Security | ✅ Fixed |
| 2 | SQL injection — array query names interpolated into SQL | Security | ✅ Fixed |
| 3 | Polly registered but never used | Misleading | ✅ Fixed |
| 4 | CI pipeline has no `dotnet test` step | Reliability | ✅ Fixed |
| 5 | Dead Newtonsoft.Json dependency | Cleanliness | ✅ Fixed |
| 6 | Reflection on every write (not cached) | Performance | ✅ Fixed |
| 7 | `NotImplementedException` explicit interface implementations | Design | ✅ Fixed |
| 8 | Manual cache expiry duplicates `IMemoryCache` | Complexity | ✅ Fixed |
| 9 | `_disposed` dead code | Dead code | ✅ Fixed |
| 10 | `new()` constraint not on interface | Design | ✅ Fixed |
| 11 | Soft delete — no ETag/optimistic concurrency | Correctness | ✅ Fixed |
| 12 | Regex count query conversion fragile | Correctness | ⬜ Open |
| 13 | Debug tests in test suite | Quality | ✅ Fixed |
| 14 | `GetAllAsync(limit, offset, count)` naming | Clarity | ⬜ Open |
| 15 | Cross-partition `GetAllAsync()` — no cost warning | Docs | ⬜ Open |
| 16 | `QueryAsync` should accept `SqlSpecification<T>` directly | API design | ⬜ Open |
