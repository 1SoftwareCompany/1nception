using One.Inception.Projections;
using System.Threading.Tasks;

namespace One.Inception;

[InceptionStartup(Bootstraps.Projections)]
internal sealed class ProjectionsStartup : IInceptionStartup /// TODO: make this <see cref="ITenantStartup"/>
{
    private readonly ProjectionBootstrapper projectionsBootstrapper;

    public ProjectionsStartup(ProjectionBootstrapper projectionsBootstrapper)
    {
        this.projectionsBootstrapper = projectionsBootstrapper;
    }

    public Task BootstrapAsync()
    {
        return projectionsBootstrapper.BootstrapAsync();
    }
}
