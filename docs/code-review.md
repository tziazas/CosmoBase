# CosmoBase Code Review

> Reviewed: 2026-05-24  
> Branch: `release/1.0.0`

## Changelog

| Date | Finding | What changed |
|------|---------|--------------|
| 2026-05-24 | #1 — SQL injection (`IN` clause) | `PropertyFilterExtensions.BuildSqlWhereClause` now emits `@{col}_{filterIdx}_in_{valueIdx}` parameters for `IN` filters instead of inlining string literals. `AddParameters` updated to bind each value. 17 unit tests added at `tests/CosmoBase.Tests/Unit/Extensions/PropertyFilterExtensionsTests.cs`. |
| 2026-05-24 | #2 — SQL injection (array query identifiers) | `CosmosValidationConstants` now exposes a compiled `SafePropertyNamePattern` regex (`^[a-zA-Z_][a-zA-Z0-9_.]*$`). `CosmosValidator.ValidateArrayPropertyQuery` validates both `arrayName` and `elementPropertyName` against this pattern before they are interpolated into SQL, rejecting anything containing spaces, quotes, semicolons, or other non-identifier characters. 35 unit tests added at `tests/CosmoBase.Tests/Unit/Validators/CosmosValidatorArrayQueryTests.cs`. |
| 2026-05-24 | #3 — Polly registered but never used | Removed `TryAddSingleton` Polly registration and `using Polly;` from `ServiceCollectionExtensions.cs`. Removed `<PackageReference Include="Polly" />` from `CosmoBase.Core.csproj` and stale release note entry. 10 DI registration unit tests added at `tests/CosmoBase.Tests/Unit/DependencyInjection/ServiceRegistrationTests.cs`. |

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

### 4. CI pipeline never runs tests
**File:** `.github/workflows/publish.yml`

The workflow chain is: restore → build → pack → publish. There is no `dotnet test` step. Broken code can be tagged and published to NuGet.

---

### 5. `NotImplementedException` thrown from explicit interface implementations
**Files:**
- `src/CosmoBase.DataServices/CosmosDataWriteService.cs:84`, `:442`
- `src/CosmoBase.DataServices/CosmosDataReadService.cs:501`

`CosmosDataWriteService` and `CosmosDataReadService` explicitly implement base interface methods (`IDataWriteService<TDto,string>.CreateAsync`, `DeleteAsync`, `IDataReadService<TDto,string>.GetByIdAsync`) by throwing exceptions at runtime. If the base interface contract can never be correctly fulfilled, the inheritance hierarchy is wrong and those base interfaces should not be in the hierarchy.

---

### 6. Reflection on every write operation
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

### 7. Dead Newtonsoft.Json dependency
**Files:**
- `src/CosmoBase.Core/CosmoBase.Core.csproj:40`
- `src/CosmoBase.Core/Repositories/CosmosRepository.cs:15`
- `src/CosmoBase.DataServices/CosmosDataWriteService.cs:7`

`CosmoBase.Core.csproj` references `Newtonsoft.Json` as a package. The `using Newtonsoft.Json;` statements in `CosmosRepository.cs` and `CosmosDataWriteService.cs` are unused — no Newtonsoft API is called anywhere. There is even a commented-out reference in the `.csproj` with the note "Remove Newtonsoft.Json unless specifically needed." The package and using directives should be removed.

---

### 8. `_disposed` field with no `IDisposable` implementation
**File:** `src/CosmoBase.Core/Repositories/CosmosRepository.cs:35`

```csharp
private bool _disposed;
```

This field is never read, never written, and the class does not implement `IDisposable`. It is dead code that implies an incomplete dispose pattern.

---

### 9. `new()` constraint mismatch between interface and implementation
**File:** `src/CosmoBase.Core/Repositories/CosmosRepository.cs:24`

```csharp
// Interface
public interface ICosmosRepository<T> where T : class, ICosmosDataModel

// Implementation — adds new() not required by the interface
public class CosmosRepository<T> : ICosmosRepository<T> where T : class, ICosmosDataModel, new()
```

The `new()` constraint is not required by the interface and does not appear to be used anywhere inside `CosmosRepository`. It silently prevents types without a parameterless constructor from using the concrete repository, even though the interface contract would allow it.

---

### 10. Manual cache expiry duplicates what `IMemoryCache` already does
**File:** `src/CosmoBase.Core/Repositories/CosmosRepository.cs:479–516`

`GetFreshCountAsync` stores a `CachedCountEntry` with a `CachedAt` timestamp. `GetCountWithCacheAsync` then manually computes the age and compares it against `cacheExpiryMinutes`. Simultaneously, the underlying cache entry is set with `AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)`.

Two parallel expiry systems are running. Setting `AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheExpiryMinutes)` on the entry directly would make the manual age-check logic and the `CachedCountEntry` wrapper unnecessary.

---

### 11. Debug tests left in the integration test suite
**File:** `tests/CosmoBase.Tests/Integration/Services/DataServicesIntegrationTests.cs`

`Debug_Container_Partition_Key` and `Debug_Partition_Key_Issue` are debugging artifacts. Both use `output.WriteLine` for ad-hoc inspection and have no meaningful assertions. They should be converted to real tests or removed.

---

### 12. Soft delete has no optimistic concurrency
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
| 4 | CI pipeline has no `dotnet test` step | Reliability | ⬜ Open |
| 5 | Dead Newtonsoft.Json dependency | Cleanliness | ⬜ Open |
| 6 | Reflection on every write (not cached) | Performance | ⬜ Open |
| 7 | `NotImplementedException` explicit interface implementations | Design | ⬜ Open |
| 8 | Manual cache expiry duplicates `IMemoryCache` | Complexity | ⬜ Open |
| 9 | `_disposed` dead code | Dead code | ⬜ Open |
| 10 | `new()` constraint not on interface | Design | ⬜ Open |
| 11 | Soft delete — no ETag/optimistic concurrency | Correctness | ⬜ Open |
| 12 | Regex count query conversion fragile | Correctness | ⬜ Open |
| 13 | Debug tests in test suite | Quality | ⬜ Open |
| 14 | `GetAllAsync(limit, offset, count)` naming | Clarity | ⬜ Open |
| 15 | Cross-partition `GetAllAsync()` — no cost warning | Docs | ⬜ Open |
| 16 | `QueryAsync` should accept `SqlSpecification<T>` directly | API design | ⬜ Open |
