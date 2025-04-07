//using System.Collections.Generic;
//using One.Inception.Projections;
//using Microsoft.Extensions.DependencyInjection;

//namespace One.Inception.Discoveries
//{
//    public class ProjectionPlayerDiscovery : DiscoveryBase<ProjectionPlayer>
//    {
//        protected override DiscoveryResult<ProjectionPlayer> DiscoverFromAssemblies(DiscoveryContext context)
//        {
//            return new DiscoveryResult<ProjectionPlayer>(GetModels());
//        }

//        IEnumerable<DiscoveredModel> GetModels()
//        {
//            yield return new DiscoveredModel(typeof(ProjectionPlayer), typeof(ProjectionPlayer), ServiceLifetime.Transient);
//        }
//    }
//}
