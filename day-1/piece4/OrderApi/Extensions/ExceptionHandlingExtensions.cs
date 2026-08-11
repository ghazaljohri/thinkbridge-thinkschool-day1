using Microsoft.AspNetCore.Diagnostics;

namespace OrderApi.Extensions;

public static class ExceptionHandlingExtensions
{
    public static void UseGlobalExceptionHandling(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception =
                    context.Features.Get<IExceptionHandlerFeature>()?.Error;

                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GlobalExceptionHandler");

                logger.LogError(
                    exception,
                    "Unhandled exception while processing request.");

                await Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "An unexpected error occurred.")
                    .ExecuteAsync(context);
            });
        });
    }
}
