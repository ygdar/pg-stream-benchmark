## Why

Стратегия **D (EfChunk)** — стоковый EF Core chunk (пачками `SaveChanges`) — по замерам на больших объёмах (1M/2M строк) выполняется на порядок дольше остальных и делает каждый полный (full/dev) прогон неоправданно долгим. Продовой вывод по этой стратегии уже сделан, держать её в наборе на каждый прогон экономически невыгодно. Её нужно убрать из запускаемого набора, чтобы сократить время циклов бенчмаркинга.

## What Changes

- Удаляется реализация стратегии D — файл `benchmark/DataAccess/EfChunkStrategy.cs` (включая `BenchDbContext`).
- Удаляется класс `EfChunkBenchmark` в `benchmark/Benchmarking/InsertBenchmarks.cs`.
- Из массива запускаемых типов в `benchmark/Program.cs` убирается `EfChunkBenchmark`; комментарии/подписи, упоминающие «5 стратегий», приводятся к 4 (A, B, C, E).
- Из метрик-маппингов убирается стратегия D:
  - `ResourceMetrics.TrafficModel.MechanismFor` — удаляется ветка `"D_EfChunk" => "SQL row-batch"`; ветка `else` схлопывается до «multi-row VALUES» (её представляет A).
  - `SummaryReport.StrategyMap` — удаляется кортеж `("EfChunk", "D_EfChunk")`.
  - `SummaryReport.StrategyRu` — удаляется запись `["D_EfChunk"]`.
- Удаляются устаревшие файлы данных `benchmark-data/metrics/D_EfChunk-*.json`.
- Обновляются текстовые/комментарные упоминания EfChunk/«EF chunk»/«5 стратегий» в коде, чтобы набор описывал только оставшиеся стратегии.

## Capabilities

### New Capabilities
- `efchunk-strategy-removal`: Набор запускаемых бенчмарков вставки SHALL содержать ровно четыре стратегии — A (пакетный INSERT), B (двоичный COPY), C (LINQ2DB BulkCopy), E (нативный server-side COPY) — и SHALL NOT содержать стратегию EfChunk (D) ни в коде стратегии, ни в наборе benchmark-классов, ни в метрик-маппингах, ни в данных прогонов.

### Modified Capabilities
<!-- Корневых спеков в openspec/specs нет; изменение является удалением части существующей функциональности и описывается новой capability. -->

## Impact

- **Код (удаление)**: `benchmark/DataAccess/EfChunkStrategy.cs` (включая `BenchDbContext`), `EfChunkBenchmark` в `benchmark/Benchmarking/InsertBenchmarks.cs`, запись в массиве типов `benchmark/Program.cs`.
- **Код (правки)**: маппинги в `benchmark/Metrics/ResourceMetrics.cs` (MechanismFor / TrafficModel) и `benchmark/Metrics/SummaryReport.cs` (StrategyMap, StrategyRu); комментарии/подписи о количестве стратегий.
- **Данные**: удаляются `benchmark-data/metrics/D_EfChunk-*.json`.
- **Зависимости (косвенно)**: `ef-core` и EF-драйвер PostgreSQL более не используются кодом стратегии; исключаются, если не задействованы иначе.
- **Поведение**: набор из 5 стратегий становится 4; полный прогон ускоряется на время, ранее тратившееся на стратегию D.