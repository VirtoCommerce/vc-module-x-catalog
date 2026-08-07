# VirtoCommerce.XCatalog.Benchmark

BenchmarkDotNet micro-benchmarks for `VirtoCommerce.XCatalog`. Mirrors the layout of
`vc-platform/benchmarks/VirtoCommerce.Platform.Benchmark`.

## Layout

Each suite lives in its own subfolder with a matching `VirtoCommerce.XCatalog.Benchmark.<Suite>`
namespace (`Caching/` today). To see what actually exists, ask the runner rather than a hand-maintained
list:

```bash
cd benchmarks/VirtoCommerce.XCatalog.Benchmark
dotnet run -c Release -- --list tree
```

## Prerequisites

- .NET 10 SDK (or whichever TFM the project currently targets).

## Running

```bash
cd benchmarks/VirtoCommerce.XCatalog.Benchmark

dotnet run -c Release -- --filter '*'                    # everything
dotnet run -c Release -- --filter '*SearchCacheKey*'     # the cache-key suite
```

Run from **this directory**. BenchmarkDotNet locates the project to rebuild relative to the working
directory, so invoking it from elsewhere with `--project` fails with
`Unable to find VirtoCommerce.XCatalog.Benchmark in <cwd>`.

## Choosing a job

`Allocated` (the `[MemoryDiagnoser]` column) is exact even on a cheap job, so for an allocation-focused
check a quick job suffices; a trustworthy time `Mean` needs the full default job. On `--job short` the
cache-key benchmarks reported a 99.9% margin of **65% of the mean** — usable for allocations, useless as
a latency figure.

```bash
dotnet run -c Release -- --filter '*SearchCacheKey*' --job short   # byte-accurate alloc, cheap
dotnet run -c Release -- --filter '*SearchCacheKey*'               # default job — trustworthy time Mean
```

## Caching / SearchCacheKeyBenchmarks

Measures `SearchProductQueryHandler.BuildSearchCacheKey` — the serialize-and-hash a request pays on every
product search, including one that searches only once and so never earns a cache hit.

The fixtures mirror the call chain of `GetIndexedSearchRequestBuilder` rather than picking builder calls
that look representative. That distinction is load-bearing: a thinner request graph does not fail, it
silently reports a smaller number. The first version of this suite omitted `AddCertainDateFilter`
(unconditional in production) and `ApplyMultiSelectFacetSearch` (which clones the filter tree onto every
aggregation), understating by 1.6x and 3–4.5x.

Two production steps are deliberately not reproduced, which makes these figures a **floor**:

- `ParseFilters` / `ParseFacets` need an `ISearchPhraseParser` and add user-supplied filters on top.
- The module pipeline (`_pipeline.Execute(builder)`) lets other modules extend the request before it is
  built.

When changing the key or the request shape, re-run and update the numbers quoted in the PR description —
nothing verifies them.
