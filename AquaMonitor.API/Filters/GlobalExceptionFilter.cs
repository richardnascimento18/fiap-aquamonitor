using System.ComponentModel.DataAnnotations;
using AquaMonitor.Application.Abstractions;
using AquaMonitor.Domain.Rules;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.Sqlite;
namespace AquaMonitor.API.Filters;

public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;
    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger) => _logger = logger;
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is ApplicationFailure failure)
        {
            context.Result = failure.PublicMessage is null ? failure.StatusCode switch
            {
                403 => new ForbidResult(),
                401 => new ChallengeResult(),
                _ => new StatusCodeResult(failure.StatusCode)
            }
                : new ObjectResult(new { message = failure.PublicMessage }) { StatusCode = failure.StatusCode };
            context.ExceptionHandled = true;
            return;
        }
        if (context.Exception is ValidationException validation)
        {
            context.Result = new BadRequestObjectResult(new { message = "Dados invalidos.", errors = new[] { validation.Message } });
            context.ExceptionHandled = true;
            return;
        }
        if (context.Exception is DomainRuleException domain)
        {
            context.Result = new BadRequestObjectResult(new { message = domain.Message });
            context.ExceptionHandled = true;
            return;
        }
        _logger.LogError(context.Exception, "Unhandled exception: {Message}", context.Exception.Message);
        var status = context.Exception is SqliteException { SqliteErrorCode: 19, SqliteExtendedErrorCode: 787 or 2067 or 1555 }
            ? StatusCodes.Status409Conflict : StatusCodes.Status500InternalServerError;
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = status == 409 ? "Conflito de integridade dos dados." : "Erro interno do servidor.",
            Detail = status == 409 ? "A operacao viola uma restricao dos dados." : "Contate o suporte."
        })
        { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
