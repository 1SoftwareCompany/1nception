using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Machine.Specifications;
using Microsoft.Extensions.DependencyInjection;
using One.Inception.MessageProcessing;
using One.Inception.Projections;
using One.Inception.Projections.Cassandra;
using One.Inception.Projections.Versioning;

namespace One.Inception.Tests.Projections;

[Subject("ProjectionRepository")]
public class When_versions_fail_to_load_for_event_sourced_projection
{
    Establish context = () => repository = new ProjectionRepositoryFixture(onLoadVersions: _ => throw new Exception("cassandra is down"));

    Because of = () => error = Catch.Exception(() => repository.SaveAsync(typeof(PersistedHandlerProjection)));

    It should_not_throw_when_versions_fail_to_load = () => error.ShouldBeNull();
    It should_not_save_anything = () => repository.Store.Saved.ShouldBeEmpty();

    static ProjectionRepositoryFixture repository;
    static Exception error;
}

[Subject("ProjectionRepository")]
public class When_versions_fail_to_load_for_projection_definition
{
    Establish context = () => repository = new ProjectionRepositoryFixture(onLoadVersions: _ => throw new Exception("cassandra is down"));

    Because of = () => error = Catch.Exception(() => repository.SaveAsync(typeof(DefinitionProjection)));

    It should_not_throw_when_versions_fail_to_load = () => error.ShouldBeNull();
    It should_not_save_anything = () => repository.Store.Saved.ShouldBeEmpty();

    static ProjectionRepositoryFixture repository;
    static Exception error;
}

[Subject("ProjectionRepository")]
public class When_loading_versions_is_canceled_for_event_sourced_projection
{
    Establish context = () => repository = new ProjectionRepositoryFixture(new CancellationToken(true), onLoadVersions: _ => throw new OperationCanceledException());

    Because of = () => error = Catch.Exception(() => repository.SaveAsync(typeof(PersistedHandlerProjection)));

    It should_not_swallow_the_cancellation = () => error.ShouldBeAssignableTo<OperationCanceledException>();

    static ProjectionRepositoryFixture repository;
    static Exception error;
}

[Subject("ProjectionRepository")]
public class When_loading_versions_is_canceled_for_projection_definition
{
    Establish context = () => repository = new ProjectionRepositoryFixture(new CancellationToken(true), onLoadVersions: _ => throw new OperationCanceledException());

    Because of = () => error = Catch.Exception(() => repository.SaveAsync(typeof(DefinitionProjection)));

    It should_not_swallow_the_cancellation = () => error.ShouldBeAssignableTo<OperationCanceledException>();

    static ProjectionRepositoryFixture repository;
    static Exception error;
}

[Subject("ProjectionRepository")]
public class When_saving_is_canceled_for_event_sourced_projection
{
    Establish context = () => repository = new ProjectionRepositoryFixture(new CancellationToken(true), ProjectionRepositoryFixture.LiveVersion, new OperationCanceledException());

    Because of = () => error = Catch.Exception(() => repository.SaveAsync(typeof(PersistedHandlerProjection)));

    It should_not_swallow_the_cancellation = () => error.ShouldBeAssignableTo<OperationCanceledException>();

    static ProjectionRepositoryFixture repository;
    static Exception error;
}

[Subject("ProjectionRepository")]
public class When_saving_fails_for_event_sourced_projection
{
    Establish context = () => repository = new ProjectionRepositoryFixture(default, ProjectionRepositoryFixture.LiveVersion, new Exception("write failed"));

    Because of = () => error = Catch.Exception(() => repository.SaveAsync(typeof(PersistedHandlerProjection)));

    It should_keep_logging_the_failure_without_throwing = () => error.ShouldBeNull();

    static ProjectionRepositoryFixture repository;
    static Exception error;
}

[Subject("ProjectionRepository")]
public class When_saving_event_sourced_projection_with_live_version
{
    Establish context = () => repository = new ProjectionRepositoryFixture(onLoadVersions: ProjectionRepositoryFixture.LiveVersion);

    Because of = () => repository.SaveAsync(typeof(PersistedHandlerProjection));

    It should_save_the_event_for_the_live_version = () => repository.Store.Saved.Count.ShouldEqual(1);

    static ProjectionRepositoryFixture repository;
}

[Subject("ProjectionRepository")]
public class When_projection_does_not_have_versions_yet
{
    Establish context = () => repository = new ProjectionRepositoryFixture(onLoadVersions: ProjectionRepositoryFixture.NoVersions);

    Because of = () => error = Catch.Exception(() => repository.SaveAsync(typeof(PersistedHandlerProjection)));

    It should_not_treat_it_as_a_failure = () => error.ShouldBeNull();
    It should_not_save_anything = () => repository.Store.Saved.ShouldBeEmpty();

    static ProjectionRepositoryFixture repository;
    static Exception error;
}

class ProjectionRepositoryFixture
{
    private readonly ProjectionRepository repository;

    public ProjectionRepositoryFixture(CancellationToken cancellationToken = default, Func<ProjectionsOperator, Task> onLoadVersions = null, Exception saveFailure = null)
    {
        Store = new ProjectionStoreStub(onLoadVersions, saveFailure);
        var accessor = new ContextAccessorStub { Context = new InceptionContext("tests", new ServiceCollection().BuildServiceProvider()) { CancellationToken = cancellationToken } };

        repository = new ProjectionRepository(accessor, Store, new HandlerFactoryStub(), new ProjectionHasher());
    }

    public ProjectionStoreStub Store { get; }

    public void SaveAsync(Type projectionType) => repository.SaveAsync(projectionType, new ProjectionSaveEvent()).GetAwaiter().GetResult();

    public static Task LiveVersion(ProjectionsOperator @operator)
    {
        string projectionName = typeof(PersistedHandlerProjection).GetContractId();
        var id = new ProjectionVersionManagerId(projectionName, "tests");
        var live = new ProjectionVersion(projectionName, ProjectionStatus.Live, 1, "hash");
        var version = new ProjectionVersion(typeof(ProjectionVersionsHandler).GetContractId(), ProjectionStatus.Live, 1, "hash");

        return @operator.OnProjectionStreamLoadedAsync(new ProjectionStream(version, id, new List<IEvent>() { new NewProjectionVersionIsNowLive(id, live) }));
    }

    public static Task NoVersions(ProjectionsOperator @operator) => @operator.OnProjectionStreamLoadedAsync(ProjectionStream.Empty());
}

class ProjectionStoreStub : IProjectionStore
{
    private readonly Func<ProjectionsOperator, Task> onLoadVersions;
    private readonly Exception saveFailure;

    public ProjectionStoreStub(Func<ProjectionsOperator, Task> onLoadVersions, Exception saveFailure)
    {
        this.onLoadVersions = onLoadVersions;
        this.saveFailure = saveFailure;
    }

    public List<ProjectionCommit> Saved { get; } = new List<ProjectionCommit>();

    public Task EnumerateProjectionsAsync(ProjectionsOperator @operator, ProjectionQueryOptions options) => onLoadVersions(@operator);

    public Task SaveAsync(ProjectionCommit commit)
    {
        if (saveFailure is not null)
            return Task.FromException(saveFailure);

        Saved.Add(commit);
        return Task.CompletedTask;
    }
}

class ContextAccessorStub : IInceptionContextAccessor
{
    public InceptionContext Context { get; set; }
}

class HandlerFactoryStub : IHandlerFactory
{
    public IHandlerInstance Create(Type handlerType) => new HandlerInstanceStub(Activator.CreateInstance(handlerType));

    class HandlerInstanceStub : IHandlerInstance
    {
        public HandlerInstanceStub(object current) => Current = current;

        public object Current { get; }

        public void Dispose() { }
    }
}

class ProjectionSaveEvent : IEvent
{
    public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;
}

[DataContract(Name = "3c0d8f6a-52ab-4d0e-9a39-6f6f6b1f7a11")]
[Projection(ProjectionEventsPersistenceSetting.Persistent, ProjectionReplaySetting.Ordered)]
class PersistedHandlerProjection : IProjection, IEventHandle<ProjectionSaveEvent>
{
    public Task HandleAsync(ProjectionSaveEvent @event, CancellationToken cancellationToken) => Task.CompletedTask;
}

[DataContract(Name = "a7b1c2d3-6e4f-4a58-8b90-1c2d3e4f5a6b")]
class DefinitionProjection : ProjectionDefinition<object, Urn>, IEventHandle<ProjectionSaveEvent>
{
    public DefinitionProjection()
    {
        Subscribe<ProjectionSaveEvent>(_ => new Urn("urn:tests:projection"));
    }

    public Task HandleAsync(ProjectionSaveEvent @event, CancellationToken cancellationToken) => Task.CompletedTask;
}
