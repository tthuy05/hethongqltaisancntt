using ItAssetManagement.Application.Mvp;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ItAssetManagement.Api.Mvp;

public static class Errors
{
    public static ProblemDetails Problem(HttpContext context, int status, string code, string message, object? errors = null)
    {
        var problem = new ProblemDetails { Status = status, Title = code, Detail = message, Type = "about:blank", Instance = context.Request.Path };
        problem.Extensions["code"] = code; problem.Extensions["traceId"] = context.TraceIdentifier;
        if (errors != null) problem.Extensions["errors"] = errors;
        return problem;
    }
    public static async Task WriteAsync(HttpContext context, int status, string code, string message, object? errors = null)
    {
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(Problem(context, status, code, message, errors), options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
    public static void Query(HttpRequest request, bool asset, bool history = false)
    {
        var allowed = history ? new[] { "page", "pageSize" } : asset
            ? ["page", "pageSize", "keyword", "status", "sortBy", "sortDirection", "assetTypeId", "departmentId"]
            : ["page", "pageSize", "keyword", "status", "sortBy", "sortDirection"];
        foreach (var item in request.Query)
            if (!allowed.Contains(item.Key) || item.Value.Count != 1) throw Validation.Invalid(item.Key, "Query parameter không hỗ trợ hoặc bị lặp.");
    }
}
public sealed class BusinessExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is BusinessException business)
            await Errors.WriteAsync(context, business.Status, business.Code, business.Message, business.Errors);
        else if (exception is BadHttpRequestException)
            await Errors.WriteAsync(context, 400, "VALIDATION_ERROR", "Request không hợp lệ.");
        else
            await Errors.WriteAsync(context, 500, "INTERNAL_ERROR", "Không thể xử lý yêu cầu.");
        return true;
    }
}
