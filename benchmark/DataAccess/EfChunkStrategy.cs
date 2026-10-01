using DbBenchmark.Domain;
using DbBenchmark.Parsing;
using Microsoft.EntityFrameworkCore;

namespace DbBenchmark.DataAccess;

/// <summary>EF Core контекст для модели <see cref="DataRecord"/>.</summary>
public sealed class BenchDbContext : DbContext
{
    public DbSet<DataRecord> Items => Set<DataRecord>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseNpgsql(BenchInfra.ConnectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataRecord>(e =>
        {
            e.ToTable(BenchInfra.TableName);
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Value).HasColumnName("value");
            e.Property(x => x.Category).HasColumnName("category");
            e.Property(x => x.Timestamp).HasColumnName("timestamp").HasColumnType("timestamp");
            e.Property(x => x.Amount).HasColumnName("amount");
        });
    }
}

/// <summary>
/// D — стоковый EF Core chunk (SaveChanges пачками). Пачки маленькие, чтобы
/// не раздувался ChangeTracker; между пачками tracker очищается.
/// </summary>
public sealed class EfChunkStrategy : IInsertStrategy
{
    public const int ChunkSize = 5000;
    public string Name => "D_EfChunk";

    public long Load(string tsvPathHost)
    {
        BenchInfra.Truncate();
        return Insert(tsvPathHost);
    }

    private static long Insert(string tsvPathHost)
    {
        using var db = new BenchDbContext();
        var buffer = new List<DataRecord>(ChunkSize);
        long total = 0;

        using var reader = new TsvRegexReader(File.OpenRead(tsvPathHost));
        foreach (var r in reader.ReadRecords())
        {
            buffer.Add(r);
            if (buffer.Count == ChunkSize)
            {
                total += Flush(db, buffer);
                buffer.Clear();
            }
        }
        if (buffer.Count > 0)
            total += Flush(db, buffer);

        return total;
    }

    private static long Flush(BenchDbContext db, List<DataRecord> rows)
    {
        db.Items.AddRange(rows);
        int n = db.SaveChanges();
        db.ChangeTracker.Clear();
        return n;
    }
}