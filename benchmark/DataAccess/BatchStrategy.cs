using System.Globalization;
using System.Text;
using DbBenchmark.Domain;
using DbBenchmark.Parsing;
using Npgsql;

namespace DbBenchmark.DataAccess;

/// <summary>
/// A — raw multi-row INSERT через npgsql (без ORM). Строки батчатся в один
/// INSERT с несколькими VALUES-кортежами и параметрами.
/// </summary>
public sealed class BatchStrategy : IInsertStrategy
{
    public const int BatchSize = 1000;
    public string Name => "A_BatchInsert";

    public long Load(string tsvPathHost)
    {
        BenchInfra.Truncate();
        using var conn = BenchInfra.Open();
        using var tx = conn.BeginTransaction();
        var buffer = new List<DataRecord>(BatchSize);
        long total = 0;

        using var reader = new TsvRegexReader(File.OpenRead(tsvPathHost));
        foreach (var r in reader.ReadRecords())
        {
            buffer.Add(r);
            if (buffer.Count == BatchSize)
            {
                total += InsertBatch(conn, tx, buffer);
                buffer.Clear();
            }
        }
        if (buffer.Count > 0)
            total += InsertBatch(conn, tx, buffer);

        tx.Commit();
        return total;
    }

    private static long InsertBatch(NpgsqlConnection conn, NpgsqlTransaction tx, List<DataRecord> rows)
    {
        var sb = new StringBuilder(
            $"INSERT INTO {BenchInfra.TableName} (id,name,value,category,timestamp,amount) VALUES ");
        var pars = new NpgsqlParameter[rows.Count * 6];
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            int b = i * 6;
            if (i > 0) sb.Append(", ");
            sb.Append($"(@p{b},@p{b + 1},@p{b + 2},@p{b + 3},@p{b + 4},@p{b + 5})");
            pars[b]     = new NpgsqlParameter($"p{b}", NpgsqlTypes.NpgsqlDbType.Integer)     { Value = r.Id };
            pars[b + 1] = new NpgsqlParameter($"p{b + 1}", NpgsqlTypes.NpgsqlDbType.Text)     { Value = r.Name };
            pars[b + 2] = new NpgsqlParameter($"p{b + 2}", NpgsqlTypes.NpgsqlDbType.Double)   { Value = r.Value };
            pars[b + 3] = new NpgsqlParameter($"p{b + 3}", NpgsqlTypes.NpgsqlDbType.Text)     { Value = r.Category };
            pars[b + 4] = new NpgsqlParameter($"p{b + 4}", NpgsqlTypes.NpgsqlDbType.Timestamp){ Value = r.Timestamp };
            pars[b + 5] = new NpgsqlParameter($"p{b + 5}", NpgsqlTypes.NpgsqlDbType.Integer)  { Value = r.Amount };
        }
        sb.Append(';');

        using var cmd = new NpgsqlCommand(sb.ToString(), conn, tx);
        cmd.Parameters.AddRange(pars);
        cmd.ExecuteNonQuery();
        return rows.Count;
    }
}