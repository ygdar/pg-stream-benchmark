using Npgsql;

namespace DbBenchmark.DataAccess;

/// <summary>
/// E — нативный server-side COPY TSV-файла средствами PostgreSQL (референс).
/// Файл должен быть смонтирован внутрь контейнера (/data, см. docker-compose) и
/// требуются права pg_read_server_files (или суперпользователь). Подготовка
/// выполняется вне тайминга. Работает с текстовым форматом COPY.
/// </summary>
public sealed class NativeCopyStrategy : IInsertStrategy
{
    public string Name => "E_NativeCopy";

    public long Load(string tsvPathHost)
    {
        BenchInfra.Truncate();

        // Имя файла из пути на хосте; внутри контейнера он доступен по /data/<file>.
        var containerPath = DatasetManager.ContainerPathFor(RowsFromFileName(tsvPathHost));

        using var conn = BenchInfra.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"COPY {BenchInfra.TableName} (id,name,value,category,timestamp,amount) " +
            $"FROM '{containerPath}' DELIMITER E'\\t';";
        cmd.ExecuteNonQuery();
        return BenchInfra.CountRows();
    }

    private static long RowsFromFileName(string path) =>
        long.Parse(Path.GetFileNameWithoutExtension(path).Split('-')[1]);
}