using One.Inception.Projections;

namespace One.Inception;

[InceptionStartup(Bootstraps.Projections)]
internal sealed class ProjectionsStartup : IInceptionStartup /// TODO: make this <see cref="ITenantStartup"/>
{
    private readonly ProjectionBootstrapper projectionsBootstrapper;

    public ProjectionsStartup(ProjectionBootstrapper projectionsBootstrapper)
    {
        this.projectionsBootstrapper = projectionsBootstrapper;
    }

    public void Bootstrap()
    {
        projectionsBootstrapper.BootstrapAsync().GetAwaiter().GetResult();
    }
}
