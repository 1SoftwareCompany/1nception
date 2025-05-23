﻿using System.Runtime.Serialization;

namespace One.Inception.Migration.Middleware.Tests.TestModel.Foo;

[DataContract(Name = "0b745bdc-0a45-4220-9dfd-804652504594")]
public class FooId : AggregateRootId
{
    FooId() { }

    public FooId(AggregateRootId id) : base(id.Tenant, "Foo", id.Id) { }

    public FooId(string id, string tenant) : base(tenant, "Foo", id) { }
}
