namespace ApiWebTrackerGanado.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = DateTime.UtcNow;

            _logger.LogInformation("Request started: {Method} {Path} at {StartTime}",
                context.Request.Method,
                context.Request.Path,
                startTime);

            await _next(context);

            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;

            _logger.LogInformation("Request completed: {Method} {Path} responded {StatusCode} in {Duration}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                duration.TotalMilliseconds);
        }
    }
}
