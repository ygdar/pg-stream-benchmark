using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using DbBenchmark.Benchmarking;

// Запуск: dotnet run -- --mode=validation|dev|full
// Сдержанный harness (мало warmup/итераций), чтобы dev/full не растягивались на часы.
string mode = "validation";
for (int i = 0; i < args.Length; i++)
    if (args[i] == "--mode" && i + 1 < args.Length) mode = args[i + 1];
    else if (args[i].StartsWith("--mode=")) mode = args[i].Substring("--mode=".Length);

RunArgs.Configure(mode);

// Абсолютный путь к каталогу данных; передаётся дочерним процессам BenchmarkDotNet через env-переменную
// (у них другой CWD), чтобы файлы генерировалась/читались всегда в одном месте (монтируется в /data).
var dataDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "benchmark-data"));
Directory.CreateDirectory(dataDir);
Environment.SetEnvironmentVariable("DBBENCH_DATA_DIR", dataDir);

RunArgs.Prepare();

// Harness: validation — быстрая проверка рабочих итераций (ShortRun);
// dev/full — меньший набор итераций, т.к. медиум/большие объёмы вставки очень ресурсоёмки
// (EF chunk и batch ~40-55 мкс/строка, на 30M строк это десятки минут на один прогон).
Job job = RunArgs.Mode switch
{
    DbBenchmark.Benchmarking.RunMode.Validation => Job.ShortRun.WithId("validation"),
    _ => Job.ShortRun
        .WithWarmupCount(1)
        .WithIterationCount(2)
        .WithId(RunArgs.Mode.ToString()),
};

var config = ManualConfig.Create(DefaultConfig.Instance).AddJob(job);

Type[] benchmarkTypes =
{
    typeof(BatchInsertBenchmark),
    typeof(RawCopyBenchmark),
    typeof(Linq2DbBulkBenchmark),
    typeof(EfChunkBenchmark),
    typeof(NativeCopyBenchmark),
};

Console.WriteLine($"=== db-benchmark run: mode={RunArgs.Mode}, sizes={string.Join(",", RunArgs.Sizes)} ===");

foreach (var t in benchmarkTypes)
    BenchmarkRunner.Run(t, config);

Console.WriteLine("Done.");