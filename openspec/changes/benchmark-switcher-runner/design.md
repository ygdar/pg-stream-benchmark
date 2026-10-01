## Context

Входная точка `benchmark/Program.cs` сейчас собирает 5 benchmark-классов в захардкоженный массив и запускает каждый через `BenchmarkRunner.Run(t, config)` в цикле. Benchmark-классы уже декларативные (`[Benchmark]`, `[GlobalSetup]`, `[ParamsSource]`). Задача — заменить цикл на `BenchmarkSwitcher`, чтобы получить нативный отбор через `--filter`/`--list` и единую точку запуска, сохранив всё остальное поведение.

## Goals / Non-Goals

**Goals:**
- Использовать `BenchmarkSwitcher.Run(args, config)` как единую точку запуска.
- Нативный отбор через `--filter` (одна/подмножество/все) и `--list flat|tree`.
- Сохранить: `--mode` → `RunArgs.Sizes`, выбор Job по режиму, `ManualConfig`, env `DBBENCH_DATA_DIR`.
- Не менять декларативные benchmark-классы.

**Non-Goals:**
- Интерактивное TUI-меню (номерной/стрелочный выбор) — не требуется, используем CLI `--filter`.
- Возможность фильтровать по размеру — размеры остаются у `--mode`.
- Рефакторинг benchmark-классов / стратегий.

## Decisions

### D1: BenchmarkSwitcher с явным списком типов
`new BenchmarkSwitcher(new[] { typeof(BatchInsertBenchmark), typeof(RawCopyBenchmark), typeof(Linq2DbBulkBenchmark), typeof(EfChunkBenchmark), typeof(NativeCopyBenchmark) })`.
*Обоснование:* типы известны и фиксированы; явный список не требует сканирования всей сборки.
*Альтернатива:* `BenchmarkSwitcher(Assembly)` — отклонено: избыточно, т.к. в сборке нет других бенчмарков.

### D2: Передача args на вход switcher
`switcher.Run(args, config)` — исходная командная строка передаётся напрямую; бинарник сам парсит `--filter`/`--list`/`--help`, а без них запускает все.
*Обоснование:* максимально использует декларативный отбор BenchmarkDotNet, минимум своего кода.
*Альтернатива:* свой разбор фильтра и ручной `BenchmarkRunner.Run` — отклонено: дублирует механику switcher.

### D3: Сохранение RunArgs и env-переменной
Перед `switcher.Run` остаются: `--mode` → `RunArgs.Configure(mode)`, установка `DBBENCH_DATA_DIR` (абсолютный путь), `RunArgs.Prepare()` (таблица + файлы), выбор Job по режиму и сборка `ManualConfig`.
*Обоснование:* switcher также спавнит дочерние процессы с другим CWD, поэтому env-путь обязателен; mode→sizes прокидывается через механизм ParamsSource/сериализацию параметров в дочерний процесс (уже работает в текущем коде).

## Risks / Trade-offs

- **[`--job` в CLI может переопределить Job по режиму]** → Задокументировать в README; по умолчанию используется `ManualConfig` с Job, выбранным по `--mode`.
- **[Фильтр не различает размеры]** → Отмечено в proposal/README: размеры выбираются `--mode`, фильтр — только стратегия. Это осознанное упрощение.
- **[Поведение по умолчанию должно остаться «все стратегии»]** → Проверяется после перехода: запуск без `--filter` выполняет все 5.

## Migration Plan

- Заменить цикл в `Program.cs` на `BenchmarkSwitcher`; сохранить подготовку (mode, env, Prepare, Job, config).
- Обновить README («Запуск»): примеры `--filter`, `--list flat`.
- Проверка: `--list flat` выводит 5 стратегий; `--filter "*RawCopy*"` запускает только её.

## Open Questions

- Нужно ли скрывать «лишние» системные опции switcher (`--join`, `--job` и т.п.) или оставить полный CLI — решается на этапе реализации (стойкость: оставляем как есть, документируем `--filter`/`--list`).