using Microsoft.Extensions.DependencyInjection;

namespace Mutation.ConsoleDI.Example;

public interface IExampleSingletonService : IReportServiceLifetime
{
	ServiceLifetime IReportServiceLifetime.Lifetime => ServiceLifetime.Singleton;
}
