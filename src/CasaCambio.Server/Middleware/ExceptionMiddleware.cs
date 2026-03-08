using System.Net;
using System.Text.Json;
using CasaCambio.Shared.Responses;

namespace CasaCambio.Server.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new ApiErrorResponse
            {
                Code = 500,
                Message = "Error interno del servidor",
                Details = context.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true
                    ? ex.ToString()
                    : null
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
