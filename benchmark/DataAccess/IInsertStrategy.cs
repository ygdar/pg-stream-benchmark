using DbBenchmark.Domain;

namespace DbBenchmark.DataAccess;

/// <summary>
/// Точка расширения: каждая стратегия вставки реализует этот интерфейс.
/// Новая стратегия — новый класс, подключается без изменения остальных.
/// </summary>
public interface IInsertStrategy
{
    /// <summary>Короткое имя для отчёта.</summary>
    string Name { get; }

    /// <summary>
    /// Загружает TSV-файл (путь на хосте) в целевую таблицу и возвращает число
    /// реально вставленных строк (или -1, если число применить нельзя — валидация
    /// тогда полагается на count(*)).
    /// </summary>
    long Load(string tsvPathHost);
}