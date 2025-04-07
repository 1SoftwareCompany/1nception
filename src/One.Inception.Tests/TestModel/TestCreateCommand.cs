using System;

namespace One.Inception.Tests.TestModel;

public class TestCreateCommand : ICommand
{
    TestCreateCommand() { }

    public TestCreateCommand(TestAggregateId id)
    {

    }

    public TestAggregateId Id { get; set; }

    public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
}
