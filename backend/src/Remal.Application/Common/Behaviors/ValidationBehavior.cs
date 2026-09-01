using FluentValidation;
using MediatR;

namespace Remal.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior — runs all registered FluentValidation validators for the request
/// before the handler executes. Throws aggregated FluentValidation.ValidationException on failure,
/// which the global exception middleware converts to RFC 7807 ProblemDetails.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f != null).ToList();
        if (failures.Count != 0) throw new ValidationException(failures);

        return await next();
    }
}
