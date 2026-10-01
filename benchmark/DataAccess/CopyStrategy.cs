using DbBenchmark.Domain;
using DbBenchmark.Parsing;
using Npgsql;

namespace DbBenchmark.DataAccess;

/// <summary>
/// B — raw BINARY COPY через npgsql (без ORM), потоковая вставка.
/// Использует двоичный протокол COPY FROM STDIN.
/// </summary>
public sealed class CopyStrategy : IInsertStrategy
{
    public string Name => "B_RawBinaryCopy";

    public long Load(string tsvPathHost)
    {
        BenchInfra.Truncate();
        using var conn = BenchInfra.Open();
        using var tx = conn.BeginTransaction();

        const string sql =
            $"COPY {BenchInfra.TableName} (id,name,value,category,timestamp,amount) FROM STDIN (FORMAT BINARY)";
        using (var writer = conn.BeginBinaryImport(sql))
        {
            using var reader = new TsvRegexReader(File.OpenRead(tsvPathHost));
            foreach (var r in reader.ReadRecords())
            {
                writer.StartRow();
                writer.Write(r.Id);
                writer.Write(r.Name);
                writer.Write(r.Value);
                writer.Write(r.Category);
                writer.Write(r.Timestamp);
                writer.Write(r.Amount);
            }
            writer.Complete();
        }

        tx.Commit();
        return BenchInfra.CountRows();
    }
}