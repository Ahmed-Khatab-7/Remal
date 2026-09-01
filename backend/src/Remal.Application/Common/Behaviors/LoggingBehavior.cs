using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Remal.Application.Common.Behaviors;

/// <summary>Logs the name + elapsed time of every MediatR request.</summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            if (sw.ElapsedMilliseconds > 500)
                _logger.LogWarning("Slow handler {Request} took {Elapsed} ms", name, sw.ElapsedMilliseconds);
            else
                _logger.LogDebug("{Request} handled in {Elapsed} ms", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "{Request} failed after {Elapsed} ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
