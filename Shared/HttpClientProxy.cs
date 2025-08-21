using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Shared;

/// <summary>
/// Прокси для HTTP клиентов - использует reflection для анализа атрибутов и создания HTTP запросов
/// </summary>
public class HttpClientProxy : DispatchProxy
{
    private HttpClient? _httpClient;
    private JsonSerializerOptions? _jsonOptions;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null || _httpClient == null)
            return CreateDefaultTask(targetMethod?.ReturnType);

        var httpRequest = CreateHttpRequest(targetMethod, args);
        return ExecuteRequestAsync(targetMethod, httpRequest);
    }

    /// <summary>
    /// Создает HTTP запрос на основе метода интерфейса
    /// </summary>
    private HttpRequestMessage CreateHttpRequest(MethodInfo method, object?[]? args)
    {
        var (httpMethod, route) = ExtractHttpMethodAndRoute(method);
        var request = new HttpRequestMessage(new HttpMethod(httpMethod), route);

        // Добавляем тело для POST/PUT запросов
        if (ShouldIncludeBody(httpMethod) && HasRequestBody(args))
        {
            request.Content = JsonContent.Create(args![0], options: _jsonOptions);
        }

        return request;
    }

    /// <summary>
    /// Извлекает HTTP метод и маршрут из атрибутов метода
    /// </summary>
    private static (string method, string route) ExtractHttpMethodAndRoute(MethodInfo method)
    {
        var httpAttributes = new (Attribute?, string)[]
        {
            (method.GetCustomAttribute<HttpGetAttribute>(), "GET"),
            (method.GetCustomAttribute<HttpPostAttribute>(), "POST"),
            (method.GetCustomAttribute<HttpPutAttribute>(), "PUT"),
            (method.GetCustomAttribute<HttpDeleteAttribute>(), "DELETE")
        };

        foreach (var (attribute, httpMethod) in httpAttributes)
        {
            if (attribute != null)
            {
                var route = GetRouteFromAttribute(attribute) ?? method.Name;
                return (httpMethod, route);
            }
        }

        return ("GET", method.Name); // Fallback
    }

    /// <summary>
    /// Извлекает маршрут из HTTP атрибута
    /// </summary>
    private static string? GetRouteFromAttribute(Attribute attribute)
    {
        return attribute switch
        {
            HttpGetAttribute get => get.Template,
            HttpPostAttribute post => post.Template,
            HttpPutAttribute put => put.Template,
            HttpDeleteAttribute delete => delete.Template,
            _ => null
        };
    }

    /// <summary>
    /// Выполняет HTTP запрос асинхронно
    /// </summary>
    private object? ExecuteRequestAsync(MethodInfo method, HttpRequestMessage request)
    {
        if (!IsTaskReturnType(method.ReturnType, out var resultType))
            return null;

        return CreateTaskWithResult(resultType, async () =>
        {
            var response = await _httpClient!.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize(json, resultType, _jsonOptions);
        });
    }

    /// <summary>
    /// Создает Task с правильным типом результата
    /// </summary>
    private static object CreateTaskWithResult(Type resultType, Func<Task<object?>> asyncOperation)
    {
        var taskCompletionSource = CreateTaskCompletionSource(resultType);

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await asyncOperation();
                SetTaskResult(taskCompletionSource, result);
            }
            catch (Exception ex)
            {
                SetTaskException(taskCompletionSource, ex);
            }
        });

        return GetTaskFromCompletionSource(taskCompletionSource);
    }

    // Вспомогательные методы для упрощения логики
    private static bool ShouldIncludeBody(string httpMethod) => httpMethod is "POST" or "PUT";
    private static bool HasRequestBody(object?[]? args) => args is { Length: > 0 } && args[0] != null;

    private static bool IsTaskReturnType(Type returnType, out Type resultType)
    {
        resultType = typeof(object);

        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
            return false;

        resultType = returnType.GetGenericArguments()[0];
        return true;
    }

    private static object? CreateDefaultTask(Type? returnType)
    {
        if (returnType != null && IsTaskReturnType(returnType, out var resultType))
        {
            return CreateTaskCompletionSource(resultType);
        }
        return null;
    }

    // Reflection helpers для работы с TaskCompletionSource
    private static object CreateTaskCompletionSource(Type resultType)
    {
        var tcsType = typeof(TaskCompletionSource<>).MakeGenericType(resultType);
        return Activator.CreateInstance(tcsType)!;
    }

    private static void SetTaskResult(object taskCompletionSource, object? result)
    {
        var method = taskCompletionSource.GetType().GetMethod("SetResult")!;
        method.Invoke(taskCompletionSource, new[] { result });
    }

    private static void SetTaskException(object taskCompletionSource, Exception exception)
    {
        var method = taskCompletionSource.GetType().GetMethod("SetException", new[] { typeof(Exception) })!;
        method.Invoke(taskCompletionSource, new object[] { exception });
    }

    private static object GetTaskFromCompletionSource(object taskCompletionSource)
    {
        var property = taskCompletionSource.GetType().GetProperty("Task")!;
        return property.GetValue(taskCompletionSource)!;
    }

    /// <summary>
    /// Инициализирует прокси с HTTP клиентом и настройками JSON
    /// </summary>
    public void Initialize(HttpClient httpClient, JsonSerializerOptions? jsonOptions = null)
    {
        _httpClient = httpClient;
        _jsonOptions = jsonOptions;
    }
}
