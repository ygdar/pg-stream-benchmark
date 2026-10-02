## 1. Конфигурация прогона (итерации)

- [x] 1.1 В `Program.cs` в не-validation ветке конфигурации Job заменить `.WithIterationCount(2)` на `.WithIterationCount(12)`, сохранив `WithWarmupCount(1)`.

## 2. CSV-разделитель

- [x] 2.1 В `Metrics/SummaryReport.cs` конфигурацию `CsvWriter` для записи `summary.csv` задать с явным `Delimiter = ","` поверх `CultureInfo.InvariantCulture`.

## 3. Проверка

- [x] 3.1 Собрать проект (`dotnet build -c Release`).
- [x] 3.2 Прогнать `dotnet run -- --mode=validation` и убедиться, что итоговый `summary.csv` в `BenchmarkDotNet.Artifacts/results/` начинается со строки «Стратегия,Строк,…» (разделитель — запятая) на любой машине.
- [x] 3.3 Обновить README, если там упоминается количество прогонов/итераций (привести к 12).