## 1. Удаление кода стратегии EfChunk

- [x] 1.1 Удалить файл `benchmark/DataAccess/EfChunkStrategy.cs` (класс `EfChunkStrategy` и `BenchDbContext`).
- [x] 1.2 Удалить класс `EfChunkBenchmark` в `benchmark/Benchmarking/InsertBenchmarks.cs` (строки ~78-81: объявление и `CreateStrategy() => new EfChunkStrategy()`).

## 2. Регистрация и комментарии

- [x] 2.1 В `benchmark/Program.cs` убрать `typeof(EfChunkBenchmark),` из массива `benchmarkTypes` (строка ~47).
- [x] 2.2 Привести комментарий «запустить все 5 стратегий» → «запустить все 4 стратегии» (строка ~56).
- [x] 2.3 Обновить комментарий о ресурсоёмкости (строка ~30), убрав упоминание «EF chunk» (оставить batch).

## 3. Чистка маппингов метрик

- [x] 3.1 В `benchmark/Metrics/ResourceMetrics.cs`: удалить ветку `"D_EfChunk"      => "SQL row-batch",` из `TrafficModel.MechanismFor` (строка ~38) и схлопнуть комментарий ветки `else` до «multi-row VALUES».
- [x] 3.2 В `benchmark/Metrics/SummaryReport.cs`: удалить кортеж `("EfChunk", "D_EfChunk"),` из `StrategyMap` (строка ~20).
- [x] 3.3 В `benchmark/Metrics/SummaryReport.cs`: удалить запись `["D_EfChunk"] = "EF Core батч (D)",` из `StrategyRu` (строка ~38).

## 4. Данные и зависимости

- [x] 4.1 Удалить устаревшие файлы данных стратегии D: `benchmark-data/metrics/D_EfChunk-1000.json`, `D_EfChunk-10000.json`, `D_EfChunk-1000000.json`, `D_EfChunk-2000000.json`.
- [x] 4.2 Выполнить проверку использования EF: `grep -rni "EntityFramework\|Npgsql.EntityFrameworkCore" benchmark/ --include=*.cs` — убедиться, что кроме удалённой стратегии никто не использует EF Core.
- [x] 4.3 Если п.4.2 не нашёл иных использований — удалить из `benchmark/DbBenchmark.csproj` пакеты `Microsoft.EntityFrameworkCore` и `Npgsql.EntityFrameworkCore.PostgreSQL`; иначе оставить с примечанием и не удалять.

## 5. Документация

- [x] 5.1 В `README.md`: убрать строку `| D | EF Core chunk (SaveChanges) ...` из таблицы стратегий (строка ~9) и изменить «5 стратегий» → «4 стратегии» (строки ~5, ~69).

## 6. Верификация

- [x] 6.1 Выполнить `grep -rni "efchunk" benchmark/ README.md benchmark-data/` — убедиться, что в коде, README и данных нет остаточных ссылок (кроме папки истории `openspec/changes/`).
- [x] 6.2 Собрать проект без ошибок (Release).
- [x] 6.3 Прогнать validation-режим без `--filter` (все 4 стратегии): для A, B, C, E формируются тайминг, ресурсные метрики и запись в сводке; сводка не содержит D и не падает.

## 7. Корректность сводки summary.md (обнаружено при верификации dev-прогона)

- [x] 7.1 Сводка по прогону перезаписывается и содержит только бенчмарки текущего прогона: перед запуском удаляются накапливающиеся `*-report.csv`, `summary.csv`, `summary.md` прошлых прогонов (`Program.cs`).
- [x] 7.2 `SummaryReport.ReadTiming` корректно парсит `Mean` в секундах и иных единицах (`s`/`ms`/`us`/`ns`), приводя к миллисекундам инвариантно.
- [x] 7.3 Проверен dev-прогон NativeCopy (1M/2M): `summary.md` содержит только «Нативный COPY (E)» с таймингом и ресурсными метриками, без устаревших строк других стратегий и без D.