using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace AquaMonitor.API.Filters;
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;
    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger) => _logger = logger;
    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled exception: {Message}", context.Exception.Message);
        var status = context.Exception is SqliteException { SqliteErrorCode: 19, SqliteExtendedErrorCode: 787 or 2067 or 1555 }
            ? StatusCodes.Status409Conflict : StatusCodes.Status500InternalServerError;
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = status == 409 ? "Conflito de integridade dos dados." : "Erro interno do servidor.",
            Detail = status == 409 ? "A operacao viola uma restricao dos dados." : "Contate o suporte."
        }) { StatusCode = status };
        context.ExceptionHandled = true;
    }
}
