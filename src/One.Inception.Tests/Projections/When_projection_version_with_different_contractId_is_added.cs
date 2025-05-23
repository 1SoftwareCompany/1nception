﻿using One.Inception.Projections;
using Machine.Specifications;

namespace One.Inception.Tests.Projections;

[Subject("Projections")]
public class When_projection_version_with_different_contractId_is_added
{
    Establish context = () =>
    {
        var building = new ProjectionVersion("buildingId", ProjectionStatus.New, 1, "buildingHash");
        versions = new ProjectionVersions(building);

        differentId = new ProjectionVersion("differentId", ProjectionStatus.New, 1, "buildingHash");
    };

    Because of = () => versions.Add(differentId);

    It should_throw_an_ArgumentException = () => versions.Count.ShouldEqual(1);

    static ProjectionVersions versions;
    static ProjectionVersion differentId;
}
