using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using One.Inception;

BenchmarkRunner.Run<UrnInitBench>();

[MemoryDiagnoser]
public class UrnInitBench
{
    string urnString = "urn:testtenant:user:8eef39fd-b4fa-4323-b062-85e756960862";

    [Benchmark]
    public void Constructor()
    {
        var urn = new Urn(urnString);
    }

    [Benchmark]
    public void Parse()
    {
        var urn = new Urn(urnString);
    }
}

