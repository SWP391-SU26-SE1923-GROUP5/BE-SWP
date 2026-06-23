using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AIStudyHub.API.Middleware;

public sealed class FluentValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var validationFailures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var argumentType = argument.GetType();
            
            // Avoid validating primitive types or strings
            if (argumentType.IsPrimitive || argumentType == typeof(string) || argumentType.IsValueType)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            var validator = context.HttpContext.RequestServices.GetService(validatorType) as IValidator;

            if (validator is not null)
            {
                var validationContext = new ValidationContext<object>(argument);
                var validationResult = await validator.ValidateAsync(validationContext);

                if (!validationResult.IsValid)
                {
                    validationFailures.AddRange(validationResult.Errors);
                }
            }
        }

        if (validationFailures.Count > 0)
        {
            throw new ValidationException(validationFailures);
        }

        await next();
    }
}
