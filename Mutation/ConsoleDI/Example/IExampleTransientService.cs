using Microsoft.Extensions.DependencyInjection;

namespace Mutation.ConsoleDI.Example;

public interface IExampleTransientService : IReportServiceLifetime
{
	ServiceLifetime IReportServiceLifetime.Lifetime => ServiceLifetime.Transient;
}
