using BenchmarkDotNet.Running;
using VirtoCommerce.XCatalog.Benchmark.Caching;

// Discovers every [Benchmark] class in this assembly. Select with --filter and choose a job with --job:
//   dotnet run -c Release -- --filter '*SearchCacheKey*'
//   dotnet run -c Release -- --filter '*' --job short
BenchmarkSwitcher.FromAssembly(typeof(SearchCacheKeyByIdsBenchmarks).Assembly).Run(args);
