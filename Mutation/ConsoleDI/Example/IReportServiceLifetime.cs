using Microsoft.Extensions.DependencyInjection;

namespace Mutation.ConsoleDI.Example;

public interface IReportServiceLifetime
{
	Guid Id { get; }

	ServiceLifetime Lifetime { get; }
}
