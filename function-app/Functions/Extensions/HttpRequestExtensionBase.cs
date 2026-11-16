using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Hip.Integrations.Shared.Functions.Extensions;

public static class HttpRequestExtensionBase
{
    public static async Task<T> GetJsonBodyAsync<T>(Stream body)
    {
        string requestBody;
        using (var streamReader = new StreamReader(body))
        {
            requestBody = await streamReader.ReadToEndAsync().ConfigureAwait(false);
        }

        T? parameters;
        try
        {
            parameters = JsonConvert.DeserializeObject<T>(requestBody);
        }
        catch (Exception ex)
        {
            throw new ValidationException($"Request body does not have the correct format. {ex.Message}");
        }

        if (parameters is null)
        {
            throw new ValidationException("Request body is null");
        }

        return parameters;
    }
}