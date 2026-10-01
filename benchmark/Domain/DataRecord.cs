using LinqToDB.Mapping;

namespace DbBenchmark.Domain;

/// <summary>
/// Единая C#-модель — единственный источник структуры данных для всех стеков
/// (EF Core, LINQ2DB, raw npgsql). Порядок свойств задаёт порядок колонок в TSV
/// и в таблице PostgreSQL. Маппинг-атрибуты linq2db описывают имя таблицы/колонок;
/// EF Core и raw-код используют собственный маппинг поверх этого же класса.
/// </summary>
[Table(Name = "bench_data")]
public sealed class DataRecord
{
    [Column(Name = "id"), PrimaryKey]
    public int Id { get; set; }

    [Column(Name = "name")]
    public string Name { get; set; } = string.Empty;

    [Column(Name = "value")]
    public double Value { get; set; }

    [Column(Name = "category")]
    public string Category { get; set; } = string.Empty;

    [Column(Name = "timestamp")]
    public DateTime Timestamp { get; set; }

    [Column(Name = "amount")]
    public int Amount { get; set; }

    /// <summary>Имена колонок в таблице/TSV. Порядок совпадает с порядком свойств.</summary>
    public static readonly string[] Columns =
        { "id", "name", "value", "category", "timestamp", "amount" };
}