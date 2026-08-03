using BenchmarkDotNet.Running;
using VirtoCommerce.XCatalog.Benchmark.Caching;

BenchmarkSwitcher.FromAssembly(typeof(SearchCacheKeyByIdsBenchmarks).Assembly).Run(args);
