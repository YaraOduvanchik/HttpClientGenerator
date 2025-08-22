namespace HttpClientGenerator.Communication.Examples;

/// <summary>
/// Пример кастомного handler для retry логики
/// </summary>
public class CustomRetryHandler : DelegatingHandler
{
    private readonly int _maxRetries = 3;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);

                // Retry только для server errors (5xx)
                if ((int)response.StatusCode < 500)
                {
                    return response;
                }

                if (attempt == _maxRetries)
                {
                    return response; // Last attempt, return as is
                }

                // Wait before retry
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
            catch (HttpRequestException) when (attempt < _maxRetries)
            {
                // Wait before retry
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
        }

        // This should never be reached, but compiler needs it
        throw new InvalidOperationException("Retry logic failed unexpectedly");
    }
}
