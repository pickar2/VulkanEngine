using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Core.Network;

public static class HttpClientHelper
{
	public static async Task<TResponse?> SendRequest<TResponse, TRequest>(this HttpClient httpClient, string url, TRequest requestData)
	{
		var ms = new MemoryStream();
		SerializerRegistry.Instance.Serialize(ms, requestData, CompressionLevel.L12_MAX);
		ms.Seek(0, SeekOrigin.Begin);

		var request = new HttpRequestMessage(HttpMethod.Post, url);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
		using var requestContent = new StreamContent(ms);
		request.Content = requestContent;
		requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
		response.EnsureSuccessStatusCode();
		var content = await response.Content.ReadAsStreamAsync();
		return SerializerRegistry.Instance.Deserialize<TResponse>(content);
	}
}
