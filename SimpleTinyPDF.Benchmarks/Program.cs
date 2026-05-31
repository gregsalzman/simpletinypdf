using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(SimpleTinyPDF.Benchmarks.CsvTableBenchmark).Assembly).Run(args);
