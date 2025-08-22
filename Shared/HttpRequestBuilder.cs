using System.Reflection;
using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace Shared;

/// <summary>
/// Строитель HTTP запросов на основе метаданных методов интерфейса
/// Обрабатывает параметры маршрута, query параметры и тело запроса
/// </summary>
public class HttpRequestBuilder
{
    private readonly MethodInfo _method;
    private readonly object?[]? _args;
    private readonly JsonSerializerOptions? _jsonOptions;

    /// <summary>
    /// Создает новый строитель HTTP запросов
    /// </summary>
    /// <param name="method">Метод интерфейса для анализа</param>
    /// <param name="args">Аргументы метода</param>
    /// <param name="jsonOptions">Настройки JSON сериализации</param>
    public HttpRequestBuilder(MethodInfo method, object?[]? args, JsonSerializerOptions? jsonOptions)
    {
        _method = method ?? throw new ArgumentNullException(nameof(method));
        _args = args;
        _jsonOptions = jsonOptions;
    }

    /// <summary>
    /// Строит HTTP запрос на основе метаданных метода
    /// </summary>
    /// <returns>Готовый HTTP запрос</returns>
    public HttpRequestMessage Build()
    {
        var (httpMethod, route) = ExtractHttpMethodAndRoute();
        var processedRoute = ProcessRouteParameters(route);
        
        var request = new HttpRequestMessage(new HttpMethod(httpMethod), processedRoute);
        
        AddRequestBody(request, httpMethod);
        AddHeaders(request);
        
        return request;
    }

    /// <summary>
    /// Извлекает HTTP метод и маршрут из атрибутов метода
    /// </summary>
    /// <returns>Кортеж с HTTP методом и маршрутом</returns>
    private (string method, string route) ExtractHttpMethodAndRoute()
    {
        var httpAttributes = new (Attribute?, string)[]
        {
            (_method.GetCustomAttribute<HttpGetAttribute>(), "GET"),
            (_method.GetCustomAttribute<HttpPostAttribute>(), "POST"),
            (_method.GetCustomAttribute<HttpPutAttribute>(), "PUT"),
            (_method.GetCustomAttribute<HttpDeleteAttribute>(), "DELETE"),
            (_method.GetCustomAttribute<HttpPatchAttribute>(), "PATCH")
        };

        foreach (var (attribute, httpMethod) in httpAttributes)
        {
            if (attribute != null)
            {
                var route = GetRouteFromAttribute(attribute) ?? _method.Name.Replace("Async", "");
                return (httpMethod, route);
            }
        }

        // Fallback - используем GET и имя метода без "Async"
        return ("GET", _method.Name.Replace("Async", ""));
    }

    /// <summary>
    /// Обрабатывает параметры маршрута и query параметры
    /// </summary>
    /// <param name="route">Исходный маршрут</param>
    /// <returns>Обработанный маршрут с подставленными параметрами</returns>
    private string ProcessRouteParameters(string route)
    {
        if (_args == null || _args.Length == 0) 
            return route;

        var parameters = _method.GetParameters();
        var processedRoute = route;
        var queryParameters = new List<string>();

        for (int i = 0; i < parameters.Length && i < _args.Length; i++)
        {
            var param = parameters[i];
            var value = _args[i];

            // Пропускаем параметры с [FromBody]
            if (param.GetCustomAttribute<FromBodyAttribute>() != null)
                continue;

            // Пропускаем параметры с [FromHeader]
            if (param.GetCustomAttribute<FromHeaderAttribute>() != null)
                continue;

            var paramName = param.Name ?? $"param{i}";
            var placeholder = $"{{{paramName}}}";

            // Обрабатываем параметры маршрута
            if (processedRoute.Contains(placeholder))
            {
                processedRoute = processedRoute.Replace(placeholder, Uri.EscapeDataString(value?.ToString() ?? ""));
            }
            else if (!HasFromBodyParameter() && !IsComplexType(param.ParameterType))
            {
                // Добавляем как query параметр если это простой тип и нет FromBody
                var queryParam = $"{paramName}={Uri.EscapeDataString(value?.ToString() ?? "")}";
                queryParameters.Add(queryParam);
            }
        }

        // Добавляем query параметры к маршруту
        if (queryParameters.Count > 0)
        {
            var separator = processedRoute.Contains('?') ? "&" : "?";
            processedRoute += separator + string.Join("&", queryParameters);
        }

        return processedRoute;
    }

    /// <summary>
    /// Добавляет тело запроса для POST/PUT/PATCH запросов
    /// </summary>
    /// <param name="request">HTTP запрос</param>
    /// <param name="httpMethod">HTTP метод</param>
    private void AddRequestBody(HttpRequestMessage request, string httpMethod)
    {
        if (!ShouldIncludeBody(httpMethod)) 
            return;

        var bodyParameter = GetBodyParameter();
        if (bodyParameter != null)
        {
            request.Content = JsonContent.Create(bodyParameter, options: _jsonOptions);
        }
    }

    /// <summary>
    /// Получает параметр, который должен быть отправлен в теле запроса
    /// </summary>
    /// <returns>Объект для сериализации в тело запроса или null</returns>
    private object? GetBodyParameter()
    {
        if (_args == null) return null;

        var parameters = _method.GetParameters();
        
        // Ищем параметр с явным атрибутом [FromBody]
        for (int i = 0; i < parameters.Length && i < _args.Length; i++)
        {
            if (parameters[i].GetCustomAttribute<FromBodyAttribute>() != null)
                return _args[i];
        }

        // Если нет явного [FromBody], для POST/PUT/PATCH берем первый сложный тип
        for (int i = 0; i < parameters.Length && i < _args.Length; i++)
        {
            var paramType = parameters[i].ParameterType;
            if (IsComplexType(paramType))
                return _args[i];
        }

        return null;
    }

    /// <summary>
    /// Добавляет заголовки к запросу на основе параметров с [FromHeader]
    /// </summary>
    /// <param name="request">HTTP запрос</param>
    private void AddHeaders(HttpRequestMessage request)
    {
        if (_args == null) return;

        var parameters = _method.GetParameters();
        
        for (int i = 0; i < parameters.Length && i < _args.Length; i++)
        {
            var param = parameters[i];
            var headerAttribute = param.GetCustomAttribute<FromHeaderAttribute>();
            
            if (headerAttribute != null)
            {
                var headerName = !string.IsNullOrEmpty(headerAttribute.Name) 
                    ? headerAttribute.Name 
                    : param.Name ?? $"header{i}";
                
                var headerValue = _args[i]?.ToString();
                if (!string.IsNullOrEmpty(headerValue))
                {
                    request.Headers.TryAddWithoutValidation(headerName, headerValue);
                }
            }
        }
    }

    /// <summary>
    /// Проверяет, есть ли в методе параметр с атрибутом [FromBody]
    /// </summary>
    /// <returns>true, если есть параметр с [FromBody]</returns>
    private bool HasFromBodyParameter()
    {
        return _method.GetParameters().Any(p => p.GetCustomAttribute<FromBodyAttribute>() != null);
    }

    /// <summary>
    /// Определяет, является ли тип сложным (не примитивным)
    /// </summary>
    /// <param name="type">Тип для проверки</param>
    /// <returns>true, если тип сложный</returns>
    private static bool IsComplexType(Type type)
    {
        // Убираем Nullable обертку
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        
        return !underlyingType.IsPrimitive 
            && underlyingType != typeof(string) 
            && underlyingType != typeof(DateTime) 
            && underlyingType != typeof(DateTimeOffset)
            && underlyingType != typeof(TimeSpan)
            && underlyingType != typeof(Guid)
            && underlyingType != typeof(decimal);
    }

    /// <summary>
    /// Определяет, должно ли тело запроса быть включено для данного HTTP метода
    /// </summary>
    /// <param name="httpMethod">HTTP метод</param>
    /// <returns>true, если тело должно быть включено</returns>
    private static bool ShouldIncludeBody(string httpMethod) => 
        httpMethod is "POST" or "PUT" or "PATCH";

    /// <summary>
    /// Извлекает маршрут из HTTP атрибута
    /// </summary>
    /// <param name="attribute">HTTP атрибут</param>
    /// <returns>Маршрут из атрибута или null</returns>
    private static string? GetRouteFromAttribute(Attribute attribute) => attribute switch
    {
        HttpGetAttribute get => get.Template,
        HttpPostAttribute post => post.Template,
        HttpPutAttribute put => put.Template,
        HttpDeleteAttribute delete => delete.Template,
        HttpPatchAttribute patch => patch.Template,
        _ => null
    };
}
