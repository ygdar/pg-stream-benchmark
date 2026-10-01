using AutoFixture;
using AutoFixture.Kernel;
using DbBenchmark.Domain;

namespace DbBenchmark.Generation;

/// <summary>
/// Детерминированная потоковая генерация строк модели на AutoFixture с фиксированным seed.
/// Собственный <see cref="ISpecimenBuilder"/> использует seeded <see cref="Random"/>,
/// поэтому один и тот же seed даёт идентичный результат; строки выдаются лениво.
/// </summary>
public sealed class DataGenerator
{
    private readonly Fixture _fixture;

    public DataGenerator(int seed = 42)
    {
        _fixture = new Fixture();
        // Наш builder перекрывает встроенные случайные builders → полная детерминированность.
        _fixture.Customizations.Add(new SeededBuilder(seed));
    }

    /// <summary>Потоково выдаёт `count` записей; Id задаётся последовательно от 0.</summary>
    public IEnumerable<DataRecord> Generate(long count)
    {
        for (long i = 0; i < count; i++)
        {
            var r = _fixture.Create<DataRecord>();
            r.Id = (int)i;
            yield return r;
        }
    }

    /// <summary>
    /// Генерирует значения простых типов модели из seeded Random. Строки не содержат
    /// табуляций/переводов строк/кавычек/backslash → файл совместим и с regex-парсером,
    /// и с текстовым форматом нативного COPY.
    /// </summary>
    private sealed class SeededBuilder : ISpecimenBuilder
    {
        private static readonly string[] NamePool =
            { "Alpha", "Beta", "Gamma", "Delta", "Epsilon" };

        private readonly Random _rnd;

        public SeededBuilder(int seed) => _rnd = new Random(seed);

        public object Create(object request, ISpecimenContext context)
        {
            if (request is Type t)
            {
                if (t == typeof(string))
                    return NamePool[_rnd.Next(NamePool.Length)] + _rnd.Next(1, 1_000_000);
                if (t == typeof(int))
                    return _rnd.Next(1, 1_000_000);
                if (t == typeof(double))
                    return Math.Round(_rnd.NextDouble() * 1000.0, 4);
                if (t == typeof(DateTime))
                    return new DateTime(2020, 1, 1).AddSeconds(_rnd.Next(0, 40_000_000));
            }
            return new NoSpecimen();
        }
    }
}