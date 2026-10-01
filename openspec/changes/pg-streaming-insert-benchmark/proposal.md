## Why

Продовой кейс — загрузка огромных TSV-файлов (десятки миллионов строк) в PostgreSQL, где наивный row-by-row ORM-инсерт слишком медленный, а файл не помещается в память. Необходимо измерить **относительный** выигрыш по времени разных стратегий вставки, чтобы обосновать выбор для прода и зафиксировать воспроизводимый бенчмарк для будущих экспериментов (в т.ч. с другими моделями данных).

## What Changes

- Создаётся единый .NET-проект (один csproj) с BenchmarkDotNet, который сравнивает по времени **относительный** выигрыш стратегий вставки одного и того же сгенерированного TSV:
  - **A** — raw multi-row INSERT через npgsql (без ORM);
  - **B** — raw BINARY COPY через npgsql (потоковая вставка, без ORM);
  - **C** — LINQ2DB `BulkCopy` с `BulkCopyType.ProviderSpecific` (BINARY COPY через npgsql);
  - **D** — стоковый EF Core chunk (SaveChanges пачками);
  - **E** — нативный серверный `COPY` TSV-файла средствами PostgreSQL (референс).
- Модель данных определяется как отдельный C# класс (`DataRecord`) и является единственным источником для всех стеков (EF, LINQ2DB, raw).
- Генерация данных — AutoFixture (фиксированный seed), сериализация в TSV — CsvHelper (только на этапе генерации).
- Парсинг TSV при чтении — source-generated регулярные выражения (`[GeneratedRegex]`), не CsvHelper.
- PostgreSQL запускается из Docker (последняя версия, максимально дефолтная конфигурация).
- Бенчмарк имеет три режима запуска: **validation** (1K/10K строк — мгновенная проверка работоспособности во время разработки), **dev** (1M/2M строк — быстрая проверка) и **full** (5M/10M/30M строк — отчёт).
- Количество строк — параметр бенчмарка: 1M, 2M, 5M, 10M, 30M.

## Capabilities

### New Capabilities
- `insert-benchmark`: Замер относительного времени различных стратегий потоковой/батчевой вставки TSV-данных в PostgreSQL (raw batch, raw COPY, LINQ2DB BulkCopy, EF chunk, нативный server-side COPY) на дефолтной конфигурации PG из Docker.

### Modified Capabilities
<!-- Нет существующих спекаций -->

## Impact

- **Источники данных**: генерируются детерминированно (AutoFixture + seed), источники истины — сгенерированный TSV-файл.
- **Зависимости (.NET)**: `benchmarkdotnet`, `npgsql`, `linq2db`, `ef-core` + EF-драйвер PG, `autofixture`, `csvhelper`.
- **Системы**: локальный PostgreSQL в Docker (docker-compose, healthcheck, volume для TSV, порт/креды `postgres/postgres`/`benchdb`).
- **Код**: новый иерархический проект (`Domain`, `Generation`, `Parsing`, `DataAccess`, `Benchmarking`) с интерфейсом `InsertStrategy` как точкой расширения.
- **Нет изменений существующего кода** — проект greenfield.