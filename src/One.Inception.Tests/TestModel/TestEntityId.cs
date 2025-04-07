using System;

namespace One.Inception.Tests.TestModel;


public class TestEntityId : EntityId<TestAggregateId>
{
    protected override ReadOnlySpan<char> EntityName => "TestEntityId";

    public TestEntityId(Guid id, TestAggregateId rootId)
        : base(id.ToString(), rootId)
    {

    }

    public TestEntityId(TestAggregateId rootId)
        : base(Guid.NewGuid().ToString(), rootId)
    {

    }
}
