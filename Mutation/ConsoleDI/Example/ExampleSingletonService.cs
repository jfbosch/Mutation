using Mutation.ConsoleDI.Example;

namespace ConsoleDI.Example;

internal sealed class ExampleSingletonService : IExampleSingletonService
{
	Guid IReportServiceLifetime.Id { get; } = Guid.NewGuid();
}
