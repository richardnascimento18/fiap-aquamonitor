using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace AquaMonitor.API.Filters;
public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;
    private readonly IHostEnvironment _env;
    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger, IHostEnvironment env)
    { _logger = logger; _env = env; }
    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled exception: {Message}", context.Exception.Message);
        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = 500,
            Title = "Erro interno do servidor.",
            Detail = _env.IsDevelopment() ? context.Exception.ToString() : "Contate o suporte."
        }) { StatusCode = 500 };
        context.ExceptionHandled = true;
    }
}
