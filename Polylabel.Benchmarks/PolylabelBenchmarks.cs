using System;
using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Polylabel;

namespace Polylabel.Benchmarks;

[MemoryDiagnoser]
public class PolylabelBenchmarks
{
    private Polygon _water1;
    private Polygon _water2;

    [GlobalSetup]
    public void Setup()
    {
        _water1 = LoadFixture("water1.json");
        _water2 = LoadFixture("water2.json");
    }

    private static Polygon LoadFixture(string filename)
    {
        string fullPath = Path.Combine(AppContext.BaseDirectory, "fixtures", filename);
        string json = File.ReadAllText(fullPath);
        double[][][] coords = JsonSerializer.Deserialize<double[][][]>(json) 
            ?? throw new Exception($"Failed to deserialize {filename}");
        return new Polygon(coords);
    }

    [Benchmark(Baseline = true)]
    public PolylabelResult NativeQueue_Water1_Precision1()
    {
        return Polylabel.Run(_water1, 1.0);
    }

    [Benchmark]
    public PolylabelResult Tinyqueue_Water1_Precision1()
    {
        return PolylabelTinyqueue.Run(_water1, 1.0);
    }

    [Benchmark]
    public PolylabelResult NativeQueue_Water2_Precision1()
    {
        return Polylabel.Run(_water2, 1.0);
    }

    [Benchmark]
    public PolylabelResult Tinyqueue_Water2_Precision1()
    {
        return PolylabelTinyqueue.Run(_water2, 1.0);
    }
}
