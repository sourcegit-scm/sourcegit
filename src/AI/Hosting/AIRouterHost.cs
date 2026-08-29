using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

using SourceGit.AI.Routing;

namespace SourceGit.AI.Hosting;

public static class AIRouterHost
{
    public static WebApplication Build(AIRouter router, AIRouterHostOptions options)
    {
        options.Validate();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(options.ListenUrl);

        var app = builder.Build();

        app.MapGet("/health", () => Results.Text("{\"status\":\"ok\"}", "application/json"));
        app.MapGet("/v1/models", () => Results.Text(
            "{\"object\":\"list\",\"data\":[{\"id\":\"all\",\"object\":\"model\",\"owned_by\":\"sourcegit\"}]}",
            "application/json"));

        app.MapPost(AIRouterApi.ChatCompletionsPath, context => ForwardAsync(context, router, options));
        app.MapPost(AIRouterApi.ResponsesPath, context => ForwardAsync(context, router, options));
        app.MapPost(AIRouterApi.ResponseAliasPath, context => ForwardAsync(context, router, options));

        return app;
    }

    private static async Task ForwardAsync(HttpContext context, AIRouter router, AIRouterHostOptions options)
    {
        if (!AIRouterApi.IsAuthorized(context.Request.Headers.Authorization.ToString(), options.ApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":{\"message\":\"Unauthorized\"}}", context.RequestAborted);
            return;
        }

        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(context.RequestAborted);

        string model;
        try
        {
            model = AIRouterApi.GetModel(payload);
        }
        catch (JsonException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":{\"message\":\"Invalid JSON payload\"}}", context.RequestAborted);
            return;
        }

        var result = await router.RouteAsync(new AIRouterRequest(model, payload), context.RequestAborted);
        context.Response.StatusCode = result.StatusCode;
        context.Response.ContentType = "application/json";

        if (!string.IsNullOrEmpty(result.Payload))
        {
            await context.Response.WriteAsync(result.Payload, context.RequestAborted);
            return;
        }

        var message = string.IsNullOrWhiteSpace(result.Error) ? "AI Router request failed." : result.Error;
        var encoded = JsonEncodedText.Encode(message).ToString();
        await context.Response.WriteAsync($"{{\"error\":{{\"message\":\"{encoded}\"}}}}", context.RequestAborted);
    }
}
