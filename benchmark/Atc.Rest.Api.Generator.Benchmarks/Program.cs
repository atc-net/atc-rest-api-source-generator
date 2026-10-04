// dotnet run -c Release --project benchmark/Atc.Rest.Api.Generator.Benchmarks -- --filter "*"
// A quick run: append --job short
//
// The benchmarks run in-process: the generator's own libraries (Atc.Rest.Api.Generator, Atc.OpenApi, ...) are not
// copied into the separate project BenchmarkDotNet would otherwise build, so the generators would fail to load.
string[] arguments = args.Contains("--inProcess", StringComparer.OrdinalIgnoreCase) ? args : [.. args, "--inProcess"];

BenchmarkSwitcher.FromAssembly(typeof(Atc.Rest.Api.Generator.Benchmarks.GeneratorBenchmarks).Assembly).Run(arguments);