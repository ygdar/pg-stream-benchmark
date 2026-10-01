## 1. Окружение и инфраструктура

- [x] 1.1 Создать единый .NET-проект (csproj) и иерархию пакетов (Domain, Generation, Parsing, DataAccess, Benchmarking)
- [x] 1.2 Добавить зависимости: benchmarkdotnet, npgsql, linq2db (+ драйвер pg для него), ef-core (+ ef pg-драйвер), autofixture, csvhelper
- [x] 1.3 Создать docker-compose.yml: PostgreSQL последней версии, дефолтная конфигурация, healthcheck, порт, volume для TSV-файла
- [x] 1.4 Добавить README.md (как поднять БД и запустить dev/full) и .gitignore

## 2. Модель и генерация

- [x] 2.1 Определить класс `DataRecord` как C#-модель, единственный источник для EF/LINQ2DB/raw (включая порядок колонок)
- [x] 2.2 Реализовать `DataGenerator` на AutoFixture с фиксированным seed (потоковая выдача строк)
- [x] 2.3 Реализовать `TsvSerializer` на CsvHelper — потоковая запись TSV-файла

## 3. Парсинг и чтение

- [x] 3.1 Реализовать `TsvRegexReader` на source-generated regex (`[GeneratedRegex]`) — потоковое чтение TSV в поля
- [x] 3.2 Убедиться, что экранирование в генерации совместимо и с regex-парсером, и с форматом нативного COPY

## 4. Стратегии вставки

- [x] 4.1 Определить интерфейс `InsertStrategy` (например `load(stream)`) как точку расширения
- [x] 4.2 Реализовать `BatchStrategy` — raw multi-row INSERT через npgsql
- [x] 4.3 Реализовать `CopyStrategy` — raw BINARY COPY через npgsql (`copyIn`)
- [x] 4.4 Верифицировать поведение LINQ2DB BulkCopy + npgsql + `BulkCopyType.ProviderSpecific` (по документации)
- [x] 4.5 Реализовать `Linq2DbBulkStrategy` — LINQ2DB BulkCopy
- [x] 4.6 Реализовать `EfChunkStrategy` — стоковый EF Core chunk SaveChanges
- [x] 4.7 Реализовать `NativeCopyStrategy` — серверный `COPY` TSV-файла (volume + `pg_read_server_files`)

## 5. BenchmarkDotNet-каркас

- [x] 5.1 Реализовать `Program.cs`: конфиг harness (сдержанные warmup/итерации), режимы запуска dev/full
- [x] 5.2 Реализовать Infra: setup/teardown, TRUNCATE между прогонами, подключение, очистка
- [x] 5.3 Реализовать генерацию/настройку данных вне тайминга (setup, а не `@Benchmark`)
- [x] 5.4 Реализовать валидацию результата (count строк == count в файле) и пометку невалидных прогонов
- [x] 5.5 Добавить классы бенчмарков: BatchInsert, RawCopy, Linq2DbBulk, EfChunk, NativeCopy
- [x] 5.6 Разнести `[Params]` для размера данных и стратегий, обеспечить режимы validation (1K/10K), dev (1M/2M) и full (5M..30M)

## 6. Проверка и прогон

- [x] 6.1 Собрать проект без ошибок
- [x] 6.2 Поднять PostgreSQL из docker-compose и дождаться готовности
- [x] 6.3 Выполнить validation-прогон (1K/10K) и убедиться, что все 5 стратегий валидно загружают данные
- [x] 6.4 Выполнить dev-прогон (1M/2M) для проверки логики на средних размерах
- [ ] 6.5 Выполнить полный прогон (5M..30M) и собрать относительные результаты