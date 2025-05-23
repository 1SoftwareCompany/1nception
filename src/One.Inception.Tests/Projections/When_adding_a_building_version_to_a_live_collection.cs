﻿using One.Inception.Projections.Versioning;
using Machine.Specifications;

namespace One.Inception.Projections;

[Subject("ProjectionVersions")]
public class When_adding_a_building_version_to_a_live_collection
{
    Establish context = () =>
    {
        initialLiveVersion = new ProjectionVersion("projectionName", ProjectionStatus.Live, 1, "hash");
        versions = new ProjectionVersions(initialLiveVersion);
        version = new ProjectionVersion("projectionName", ProjectionStatus.New, 2, "hash");
    };

    Because of = () => versions.Add(version);

    It should_have_next_version = () => versions.GetNext(new MarkupInterfaceProjectionVersioningPolicy(), "hash").ShouldNotEqual(version);
    It should_have_live_version = () => versions.GetLive().ShouldNotBeNull();
    It should_have_correct_live_version = () => versions.GetLive().ShouldEqual(initialLiveVersion);

    It should_not_be__canceled__ = () => versions.IsCanceled(version).ShouldBeFalse();
    It should_not_be__outdated__ = () => versions.IsOutdated(version).ShouldBeFalse();

    static ProjectionVersion initialLiveVersion;
    static ProjectionVersion version;
    static ProjectionVersions versions;
}
