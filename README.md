# db-benchmark

Бенчмарк относительной скорости стратегий потоковой/батчевой вставки TSV-данных в PostgreSQL на языке C# (.NET).

Сравниваются 5 стратегий загрузки **одного и того же** сгенерированного TSV-файла:

| ID | Стратегия | Проводной механизм | ORM |
|----|-----------|--------------------|-----|
| A | raw multi-row INSERT (npgsql)          | SQL multi-row VALUES | нет |
| B | raw BINARY COPY (npgsql `copyIn`)      | COPY (binary)       | нет |
| C | LINQ2DB BulkCopy (`ProviderSpecific`)  | COPY (binary)       | linq2db |
| D | EF Core chunk (SaveChanges)            | SQL row-batch       | EF Core |
| E | нативный server-side `COPY`            | COPY (text, сервер) | PG |

Цель — **относительный** выигрыш по времени, а не абсолютные значения (окружение: Windows + PostgreSQL в Docker через loopback).

## Стек и ограничения

- Весь проект реализуется **только на C# (.NET)** — код, зависимости (NuGet), скрипты и документация.
- **Java категорически запрещена** во всём репозитории: Java/JVM-код, Kotlin, Groovy, Scala, сборка через Maven/Gradle, Java-библиотеки и Java-инструменты не допускаются ни в каком виде.
- **linq2db** — это **только и исключительно C#-библиотека** доступа к данным. Официальный источник: <https://github.com/linq2db/linq2db>. Она подключается как .NET (NuGet) пакет, вызывается из C#-кода и **не имеет никакого отношения к Java/JVM**.

## Требования

- .NET SDK 9
- Docker (или Docker Desktop на Windows)
- PostgreSQL из `docker-compose.yml` (последняя версия, дефолтная конфигурация)

## Запуск

### 1. Поднять БД

```bash
docker compose up -d
# дождаться готовности (healthcheck): pg_isready
docker compose ps
```

По умолчанию: `postgres://postgres:postgres@localhost:5432/benchdb`.

### 2. Запустить бенчмарк

```bash
# validation — быстрая проверка работоспособности, файлы 1K/10K строк
dotnet run -- --mode=validation

# dev — проверка логики, файлы 1M/2M
dotnet run -- --mode=dev

# full — отчёт, файлы 5M/10M/30M
dotnet run -- --mode=full
```

Отбор стратегий выполняется через BenchmarkDotNet (`BenchmarkSwitcher`).
Размеры данных выбираются только `--mode`; фильтр `--filter` выбирает **стратегии** (не размеры).

```bash
# только стратегия B (RawCopy) на размерах validation
dotnet run -- --mode=validation --filter "*RawCopy*"

# подмножество стратегий: A (BatchInsert) и E (NativeCopy)
dotnet run -- --mode=dev --filter "*BatchInsert*|*NativeCopy*"

# список доступных бенчмарков без запуска
dotnet run -- --list flat
# (или --list tree — дерево)
```

Без `--filter` выполняются все 5 стратегий на размерах выбранного `--mode`.

Генерация TSV выполняется в `benchmark-data/` (смонтировано в контейнер как `/data` для нативного COPY).

## Структура

```
docker-compose.yml     # PG: дефолт, healthcheck, volume для TSV
domain/                # DataRecord — модель = единый C# класс
generation/            # AutoFixture (генерация) + CsvHelper (TSV)
parsing/               # TsvRegexReader — source-generated regex
dataaccess/            # InsertStrategy + 5 реализаций
benchmarking/          # BenchmarkDotNet + инфраструктура
```

## Валидация

Каждый прогон проверяет `count(таблица) == count(файл)`; невалидный прогон помечается и не учитывается.