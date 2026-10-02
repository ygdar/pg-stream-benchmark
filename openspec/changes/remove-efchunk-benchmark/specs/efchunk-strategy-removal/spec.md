## ADDED Requirements

### Requirement: Набор бенчмарков не содержит стратегию EfChunk
Комплект запускаемых бенчмарков вставки SHALL содержать ровно четыре стратегии — A (пакетный INSERT), B (двоичный COPY), C (LINQ2DB BulkCopy), E (нативный server-side COPY) — и SHALL NOT содержать стратегию EfChunk (D). Стратегия D SHALL быть удалена из кода стратегии, из набора benchmark-классов, из маппингов метрик и из данных прогонов.

#### Scenario: Набор из четырёх стратегий
- **WHEN** происходит запуск бенчмарков через `Program.cs`
- **THEN** массив запускаемых benchmark-типов включает только `BatchInsertBenchmark`, `RawCopyBenchmark`, `Linq2DbBulkBenchmark` и `NativeCopyBenchmark` и не содержит `EfChunkBenchmark`

#### Scenario: Нет кода стратегии EfChunk
- **WHEN** реализация проверяется
- **THEN** файл `DataAccess/EfChunkStrategy.cs` (включая `BenchDbContext`) отсутствует в кодовой базе

#### Scenario: Нет маппинга стратегии EfChunk в трафик-модели
- **WHEN** вычисляется проводной механизм стратегии
- **THEN** `ResourceMetrics.TrafficModel.MechanismFor` не содержит ветки `"D_EfChunk"` и ветка `else` описывает только `multi-row VALUES`

#### Scenario: Нет маппинга стратегии EfChunk в сводке
- **WHEN** формируется отчёт-сводка
- **THEN** `SummaryReport.StrategyMap` и `SummaryReport.StrategyRu` не содержат записей для EfChunk/`D_EfChunk`

#### Scenario: Нет данных прошлых прогонов EfChunk
- **WHEN** проверяется каталог данных метрик
- **THEN** в `benchmark-data/metrics/` отсутствуют файлы `D_EfChunk-*.json`

### Requirement: Оставшиеся стратегии сохраняют поведение
Удаление стратегии EfChunk SHALL NOT изменять поведение, метрики и формат отчётов оставшихся стратегий A, B, C и E.

#### Scenario: Оставшиеся стратегии продолжают отчитываться
- **WHEN** выполняется validation-прогон
- **THEN** для каждой из стратегий A, B, C, E по-прежнему формируются тайминг, ресурсные метрики и запись в сводке

#### Scenario: Сводка формируется без стратегии D
- **WHEN** формируется общий отчёт прогона
- **THEN** сводка содержит только стратегии A, B, C, E и не падает из-за отсутствия данных по D

#### Scenario: Сводка отражает только текущий прогон
- **WHEN** выполняется прогон с отбором по стратегии/размеру (например, dev для NativeCopy)
- **THEN** сводка SHALL содержать только бенчмарки текущего прогона с корректным временем (в т.ч. секундной точности из dev/full-режимов) и SHALL NOT содержать устаревшие строки предыдущих прогонов