using Npgsql;

namespace DbBenchmark.DataAccess;

/// <summary>
/// Инфраструктура БД: создание таблицы, очистка между прогонами, count, подключение.
/// Вся настройка выполняется вне тайминга бенчмарка.
/// </summary>
public static class BenchInfra
{
    public const string TableName = "bench_data";

    // Windows + PG в Docker через loopback (см. docker-compose.yml).
    public const string ConnectionString =
        "Host=localhost;Port=5432;Database=benchdb;Username=postgres;Password=postgres;Pooling=true;";

    public static NpgsqlConnection Open()
    {
        var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    /// <summary>Создаёт целевую таблицу, если её ещё нет (вне тайминга).</summary>
    public static void EnsureDatabase()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"CREATE TABLE IF NOT EXISTS {TableName} (" +
            "id int NOT NULL PRIMARY KEY, " +
            "name text NOT NULL, " +
            "value double precision NOT NULL, " +
            "category text NOT NULL, " +
            "timestamp timestamp NOT NULL, " +
            "amount int NOT NULL);";
        cmd.ExecuteNonQuery();
    }

    /// <summary>Очищает таблицу перед новым прогоном. Вне тайминга.</summary>
    public static void Truncate()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"TRUNCATE {TableName};";
        cmd.ExecuteNonQuery();
    }

    public static long CountRows()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT count(*) FROM {TableName};";
        return (long)cmd.ExecuteScalar()!;
    }
}