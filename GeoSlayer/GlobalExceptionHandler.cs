using GeoSlayer.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GeoSlayer
{
    // Implements IExceptionHandler (ASP.NET Core .NET 8+) to centralise exception-to-HTTP-response
    // mapping for all known/expected exceptions. Registered in Program.cs via
    // AddExceptionHandler<GlobalExceptionHandler>() and activated by app.UseExceptionHandler().
    //
    // To handle a new custom exception:
    //   1. Create the exception class under /Exceptions
    //   2. Add a single entry to _exceptionMap below with the desired status code and user-facing title
    //
    // Any exception type NOT present in the map returns false from TryHandleAsync, which tells
    // the middleware pipeline this handler doesn't own it. Those exceptions fall through to
    // AddProblemDetails() which returns a generic 500 with no internal details exposed.
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private static readonly Dictionary<Type, (int StatusCode, string Title)> _exceptionMap = new()
        {
            { typeof(NotFoundException), (StatusCodes.Status404NotFound, "Not Found") },
            { typeof(BadRequestException), (StatusCodes.Status400BadRequest, "Bad Request") },
            { typeof(ConflictException), (StatusCodes.Status409Conflict, "Conflict") },
            { typeof(UnauthorizedException), (StatusCodes.Status401Unauthorized, "Unauthorized") },
        };

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (!_exceptionMap.TryGetValue(exception.GetType(), out var mapped))
            {
                return false;
            }

            httpContext.Response.StatusCode = mapped.StatusCode;

            // ProblemDetails is the RFC 7807 standard for HTTP error responses.
            // Title comes from the map (human-readable, safe to expose in production).
            // Detail comes from the exception message (should always be a user-facing message,
            // never a raw stack trace or internal system detail).
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = mapped.StatusCode,
                Title = mapped.Title,
                Detail = exception.Message
            }, cancellationToken);

            return true;
        }
    }
}
