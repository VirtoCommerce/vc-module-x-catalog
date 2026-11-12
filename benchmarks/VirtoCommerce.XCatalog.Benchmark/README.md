# VirtoCommerce.XCatalog.Benchmark

BenchmarkDotNet micro-benchmarks for `VirtoCommerce.XCatalog`. Mirrors the layout of
`vc-platform/benchmarks/VirtoCommerce.Platform.Benchmark`.

## Layout

Each suite lives in its own subfolder with a matching `VirtoCommerce.XCatalog.Benchmark.<Suite>`
namespace (`Caching/`, `Binding/` today). To see what actually exists, ask the runner rather than a hand-maintained
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

## Binding / ProductBindingBenchmarks

Measures binding **one** indexed product document — what `SearchProductQueryHandler.ConvertProducts` does
per hit through `XCatalogMapper.ToExpProduct`, i.e. `new GenericModelBinder<ExpProduct>().BindModel(document)`.
Three arms over the same document: `BindExpProduct` (the production call, baseline), and the two binders
that dominate it, `BindCatalogProduct` (field `__object`) and `BindMinVariationPrices` (field
`__minvariationprice`), so a change in the total can be attributed. `Allocated` is the primary figure.

The document is shaped like an Elasticsearch 8 provider's: `__object` is a string written by
`ProductJsonSerializer`, `__minvariationprice` an array of such strings (three currencies), next to the
scalar fields a stored document carries. The synthetic product (properties, images, assets, outlines, SEO
infos, reviews, category links) is sized so its serialized `__object` is about 20 000 characters, the length
measured on a real stored object; the actual length is printed when the run starts. To measure a real
document instead, point `XCATALOG_BENCH_OBJECT_JSON` at a file holding a stored `__object` — never commit
such a file:

```bash
XCATALOG_BENCH_OBJECT_JSON=~/object.json dotnet run -c Release -- --filter '*ProductBinding*'
```

Not reproduced, so the figures are per-document binding cost, not request cost:

- a provider that returns `__object` as a `JObject` takes another branch of `CatalogProductBinder`;
- real documents vary in size; one fixed size stands in for them;
- the rest of the Elasticsearch response materialisation and everything after binding.

The file uses only public API that exists both before and after a change to the binders, so it is meant to be
run on **two code states with the same file** — copy it into a checkout of the base commit, run, then run it on
the change — and the two `Allocated` columns compared.
