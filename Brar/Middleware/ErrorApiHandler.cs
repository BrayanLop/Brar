using Brar.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Brar.Middleware;

/// <summary>Traduce <see cref="ErrorApi"/> a ProblemDetails con su status; el mensaje va en title y detail.</summary>
public sealed class ErrorApiHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ErrorApi error)
            return false;

        httpContext.Response.StatusCode = error.Status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = error.Status, Title = error.Message, Detail = error.Message },
            cancellationToken);
        return true;
    }
}
