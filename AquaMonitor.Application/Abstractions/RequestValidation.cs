using System.ComponentModel.DataAnnotations;

namespace AquaMonitor.Application.Abstractions;

internal static class RequestValidation
{
    public static void Validate(object request) => Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
    public static void Page(int page, int pageSize, int maximum)
    {
        if (page < 1 || pageSize < 1 || pageSize > maximum)
            throw new ApplicationFailure(400, "Dados invalidos.");
    }
}
