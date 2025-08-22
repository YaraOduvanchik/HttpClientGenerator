using System.Reflection;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shared.Handlers;

namespace Shared;

/// <summary>
/// Прокси для HTTP клиентов - использует reflection для анализа атрибутов и создания HTTP запросов
/// Обеспечивает автоматическое преобразование вызовов методов интерфейса в HTTP запросы
/// </summary>
public class HttpClientProxy : DispatchProxy
{
    private HttpClient? _httpClient;
    private JsonSerializerOptions? _jsonOptions;
    private ILogger<HttpClientProxy>? _logger;

    // Кеш для хранения метаданных методов для улучшения производительности
    private static readonly ConcurrentDictionary<string, CachedMethodInfo> _methodCache = new();

    /// <summary>
    /// Метаданные метода для кеширования
    /// </summary>
    private class CachedMethodInfo
    {
        public Type ReturnType { get; init; } = typeof(object);
        public Type? ResultType { get; init; }
        public bool IsTaskReturnType { get; init; }
        public string MethodKey { get; init; } = string.Empty;
    }

    /// <summary>
    /// Перехватывает вызовы методов интерфейса и преобразует их в HTTP запросы
    /// </summary>
    /// <param name="targetMethod">Вызываемый метод</param>
    /// <param name="args">Аргументы метода</param>
    /// <returns>Результат HTTP запроса</returns>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null || _httpClient == null)
        {
            _logger?.LogWarning("Method or HttpClient is null, returning default task");
            return CreateDefaultTask(targetMethod?.ReturnType);
        }

        try
        {
            var cachedInfo = GetOrCreateCachedMethodInfo(targetMethod);
            
            var requestBuilder = new HttpRequestBuilder(targetMethod, args, _jsonOptions);
            var request = requestBuilder.Build();
            
            _logger?.LogDebug("Executing HTTP request for method {Method}", targetMethod.Name);
            
            return ExecuteRequestAsync(cachedInfo, request);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating HTTP request for method {Method}", targetMethod.Name);
            throw;
        }
    }

    /// <summary>
    /// Получает или создает кешированную информацию о методе
    /// </summary>
    /// <param name="method">Метод для анализа</param>
    /// <returns>Кешированная информация о методе</returns>
    private static CachedMethodInfo GetOrCreateCachedMethodInfo(MethodInfo method)
    {
        var key = $"{method.DeclaringType?.FullName}.{method.Name}";
        
        return _methodCache.GetOrAdd(key, _ =>
        {
            var isTaskReturnType = IsTaskReturnType(method.ReturnType, out var resultType);
            
            return new CachedMethodInfo
            {
                ReturnType = method.ReturnType,
                ResultType = resultType,
                IsTaskReturnType = isTaskReturnType,
                MethodKey = key
            };
        });
    }

    /// <summary>
    /// Выполняет HTTP запрос асинхронно
    /// </summary>
    /// <param name="methodInfo">Кешированная информация о методе</param>
    /// <param name="request">HTTP запрос</param>
    /// <returns>Результат выполнения запроса</returns>
    private object? ExecuteRequestAsync(CachedMethodInfo methodInfo, HttpRequestMessage request)
    {
        if (!methodInfo.IsTaskReturnType || methodInfo.ResultType == null)
        {
            _logger?.LogWarning("Method {MethodKey} has invalid return type", methodInfo.MethodKey);
            return CreateDefaultTask(methodInfo.ReturnType);
        }

        return CreateTaskWithResult(methodInfo.ResultType, async () =>
        {
            try
            {
                _logger?.LogDebug("Sending HTTP request {Method} {Uri}", 
                    request.Method, request.RequestUri);

                var response = await _httpClient!.SendAsync(request);
                
                _logger?.LogDebug("Received HTTP response {StatusCode} for {Method} {Uri}", 
                    response.StatusCode, request.Method, request.RequestUri);

                // Возвращаем сам HttpResponseMessage если это требуется
                if (methodInfo.ResultType == typeof(HttpResponseMessage))
                    return response;

                // Для успешных ответов десериализуем содержимое
                if (response.IsSuccessStatusCode)
                {
                    return await DeserializeResponse(response, methodInfo.ResultType);
                }

                // ExceptionHandler должен обработать ошибочные статус коды
                response.EnsureSuccessStatusCode();
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing HTTP request for {MethodKey}", methodInfo.MethodKey);
                throw;
            }
            finally
            {
                request.Dispose();
            }
        });
    }

    /// <summary>
    /// Десериализует ответ HTTP запроса в нужный тип
    /// </summary>
    /// <param name="response">HTTP ответ</param>
    /// <param name="resultType">Целевой тип для десериализации</param>
    /// <returns>Десериализованный объект</returns>
    private async Task<object?> DeserializeResponse(HttpResponseMessage response, Type resultType)
    {
        try
        {
            // Обработка специальных типов
            if (resultType == typeof(string))
            {
                return await response.Content.ReadAsStringAsync();
            }

            if (resultType == typeof(byte[]))
            {
                return await response.Content.ReadAsByteArrayAsync();
            }

            if (resultType == typeof(Stream))
            {
                return await response.Content.ReadAsStreamAsync();
            }

            // Для void методов возвращаем null
            if (resultType == typeof(object) || resultType == typeof(void))
            {
                return null;
            }

            // Проверяем наличие содержимого
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(contentType))
            {
                _logger?.LogWarning("Response has no content type, returning null");
                return GetDefaultValue(resultType);
            }

            // Десериализация JSON
            if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                var json = await response.Content.ReadAsStringAsync();
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    return GetDefaultValue(resultType);
                }

                return JsonSerializer.Deserialize(json, resultType, _jsonOptions);
            }

            // Для других типов контента возвращаем строку
            _logger?.LogWarning("Unsupported content type {ContentType}, returning as string", contentType);
            return await response.Content.ReadAsStringAsync();
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Failed to deserialize response to type {Type}", resultType.Name);
            throw new HttpClientException($"Failed to deserialize response to {resultType.Name}", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during response deserialization");
            throw;
        }
    }

    /// <summary>
    /// Получает значение по умолчанию для типа
    /// </summary>
    /// <param name="type">Тип</param>
    /// <returns>Значение по умолчанию</returns>
    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    /// <summary>
    /// Создает Task с правильным типом результата
    /// </summary>
    /// <param name="resultType">Тип результата</param>
    /// <param name="asyncOperation">Асинхронная операция</param>
    /// <returns>Task с правильным типом</returns>
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

    /// <summary>
    /// Проверяет, является ли тип возвращаемого значения Task<T>
    /// </summary>
    /// <param name="returnType">Тип возвращаемого значения</param>
    /// <param name="resultType">Тип результата внутри Task</param>
    /// <returns>true, если это Task<T></returns>
    private static bool IsTaskReturnType(Type returnType, out Type resultType)
    {
        resultType = typeof(object);

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            resultType = returnType.GetGenericArguments()[0];
            return true;
        }

        if (returnType == typeof(Task))
        {
            resultType = typeof(void);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Создает Task по умолчанию для случаев ошибок
    /// </summary>
    /// <param name="returnType">Ожидаемый тип возвращаемого значения</param>
    /// <returns>Task по умолчанию</returns>
    private static object? CreateDefaultTask(Type? returnType)
    {
        if (returnType != null && IsTaskReturnType(returnType, out var resultType))
        {
            var taskCompletionSource = CreateTaskCompletionSource(resultType);
            SetTaskResult(taskCompletionSource, GetDefaultValue(resultType));
            return GetTaskFromCompletionSource(taskCompletionSource);
        }
        return null;
    }

    /// <summary>
    /// Создает TaskCompletionSource для указанного типа результата
    /// </summary>
    /// <param name="resultType">Тип результата</param>
    /// <returns>TaskCompletionSource</returns>
    private static object CreateTaskCompletionSource(Type resultType)
    {
        var tcsType = typeof(TaskCompletionSource<>).MakeGenericType(resultType);
        return Activator.CreateInstance(tcsType)!;
    }

    /// <summary>
    /// Устанавливает результат в TaskCompletionSource
    /// </summary>
    /// <param name="taskCompletionSource">TaskCompletionSource</param>
    /// <param name="result">Результат</param>
    private static void SetTaskResult(object taskCompletionSource, object? result)
    {
        var method = taskCompletionSource.GetType().GetMethod("SetResult")!;
        method.Invoke(taskCompletionSource, new[] { result });
    }

    /// <summary>
    /// Устанавливает исключение в TaskCompletionSource
    /// </summary>
    /// <param name="taskCompletionSource">TaskCompletionSource</param>
    /// <param name="exception">Исключение</param>
    private static void SetTaskException(object taskCompletionSource, Exception exception)
    {
        var method = taskCompletionSource.GetType().GetMethod("SetException", new[] { typeof(Exception) })!;
        method.Invoke(taskCompletionSource, new object[] { exception });
    }

    /// <summary>
    /// Получает Task из TaskCompletionSource
    /// </summary>
    /// <param name="taskCompletionSource">TaskCompletionSource</param>
    /// <returns>Task</returns>
    private static object GetTaskFromCompletionSource(object taskCompletionSource)
    {
        var property = taskCompletionSource.GetType().GetProperty("Task")!;
        return property.GetValue(taskCompletionSource)!;
    }

    /// <summary>
    /// Инициализирует прокси с HTTP клиентом и настройками
    /// </summary>
    /// <param name="httpClient">HTTP клиент</param>
    /// <param name="jsonOptions">Настройки JSON сериализации</param>
    /// <param name="logger">Логгер</param>
    public void Initialize(HttpClient httpClient, JsonSerializerOptions? jsonOptions = null, ILogger<HttpClientProxy>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _jsonOptions = jsonOptions;
        _logger = logger;
    }
}
