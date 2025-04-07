using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace One.Inception;

public class ServiceCollectionMock : List<ServiceDescriptor>, IServiceCollection { }
