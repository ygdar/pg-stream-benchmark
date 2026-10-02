using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using DbBenchmark.Benchmarking;
using DbBenchmark.Metrics;

// Запуск: dotnet run -- --mode=validation|dev|full [--filter "*<Стратегия>*" | --list flat]
// Отбор и запуск бенчмарков делегируется BenchmarkSwitcher (см. ниже).
string mode = "validation";
var bdnArgs = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--mode") { mode = (i + 1 < args.Length) ? args[i + 1] : mode; i++; continue; }
    else if (args[i].StartsWith("--mode=")) { mode = args[i].Substring("--mode=".Length); continue; }
    bdnArgs.Add(args[i]);
}

RunArgs.Configure(mode);

// Абсолютный путь к каталогу данных; передаётся дочерним процессам BenchmarkDotNet через env-переменную
// (у них другой CWD), чтобы файлы генерировалась/читались всегда в одном месте (монтируется в /data).
var dataDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "benchmark-data"));
Directory.CreateDirectory(dataDir);
Environment.SetEnvironmentVariable("DBBENCH_DATA_DIR", dataDir);

RunArgs.Prepare();

// Harness: validation — быстрая проверка рабочих итераций (ShortRun);
// dev/full — меньший набор итераций, т.к. медиум/большие объёмы вставки очень ресурсоёмки
// (batch ~40-55 мкс/строка, на 30M строк это десятки минут на один прогон).
Job job = RunArgs.Mode switch
{
    DbBenchmark.Benchmarking.RunMode.Validation => Job.ShortRun.WithId("validation"),
    _ => Job.ShortRun
        .WithWarmupCount(1)
        .WithIterationCount(12)
        .WithId(RunArgs.Mode.ToString()),
};

var config = ManualConfig.Create(DefaultConfig.Instance).AddJob(job);

Type[] benchmarkTypes =
{
    typeof(BatchInsertBenchmark),
    typeof(RawCopyBenchmark),
    typeof(Linq2DbBulkBenchmark),
    typeof(NativeCopyBenchmark),
};

Console.WriteLine($"=== db-benchmark run: mode={RunArgs.Mode}, sizes={string.Join(",", RunArgs.Sizes)} ===");

// Всё управление запуском/отбором стратегий отдаётся BenchmarkSwitcher:
//   --filter "*<Стратегия>*"  — запустить выбранные стратегии;
//   --list flat|tree          — вывести список без запуска;
//   (без флагов отбора)       — запустить все 4 стратегии.
// Подчищаем отчёты и сводки предыдущих прогонов, чтобы сводка содержала только
// бенчмарки текущего прогона (при отборе через --filter/разные режимы файлы
// `*-report.csv` из прошлых запусков иначе накапливаются и попадают в сводку).
var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts", "results");
if (Directory.Exists(resultsDir))
    foreach (var f in Directory.EnumerateFiles(resultsDir))
        if (f.EndsWith("-report.csv", StringComparison.OrdinalIgnoreCase)
            || f.EndsWith("summary.csv", StringComparison.OrdinalIgnoreCase)
            || f.EndsWith("summary.md", StringComparison.OrdinalIgnoreCase))
            File.Delete(f);

// При этом без --filter/--list BenchmarkSwitcher уходит в интерактивный выбор
// и ждёт stdin (`--filter *` явно означает «все»).
var switcher = new BenchmarkSwitcher(benchmarkTypes);
if (bdnArgs.Count == 0 || bdnArgs.All(a => !a.StartsWith("--filter") && !a.StartsWith("--list")))
    switcher.Run(new[] { "--filter", "*" }, config);
else
    switcher.Run(bdnArgs.ToArray(), config);

// Единый отчёт-сводка по всем запущенным бенчмаркам текущего прогона.
SummaryReport.Generate();

Console.WriteLine("Done.");