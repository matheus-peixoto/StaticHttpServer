using System.Net;
using System.Text;

namespace StaticFileServer.Http.Aggregates;

public class HttpResponseInfo
{
    internal HttpResponseInfo(HttpStatusCode statusCode, int lenght)
    {
        StatusCode = statusCode;
        Lenght = lenght;
    }

    internal HttpResponseInfo(HttpStatusCode statusCode, string contentType, int lenght)
    {
        StatusCode = statusCode;
        ContentType = contentType;
        Lenght = lenght;
    }

    public HttpStatusCode StatusCode { get; }

    public int Lenght { get; }

    public string? ContentType { get; }

    public Encoding Encoding { get; } = Encoding.UTF8;
}
