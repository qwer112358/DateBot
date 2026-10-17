using System.Net;
using System.Text;

namespace DateBot.Infrastructure.Tests;

/// <summary>Answers HTTP requests from a function and remembers what was asked.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<(HttpMethod Method, string PathAndQuery, string? InitData, string Body)> Requests { get; } = [];

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        foreach (var (name, value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var initData = request.Headers.TryGetValues(MiniApp.TashdateAppClient.InitDataHeader, out var values) ? values.Single() : null;
        Requests.Add((request.Method, request.RequestUri!.PathAndQuery, initData, body));
        return respond(request, body);
    }
}
