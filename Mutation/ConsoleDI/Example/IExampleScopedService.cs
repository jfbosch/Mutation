using Microsoft.Extensions.DependencyInjection;

namespace Mutation.ConsoleDI.Example;

public interface IExampleScopedService : IReportServiceLifetime
{
	ServiceLifetime IReportServiceLifetime.Lifetime => ServiceLifetime.Scoped;
}
