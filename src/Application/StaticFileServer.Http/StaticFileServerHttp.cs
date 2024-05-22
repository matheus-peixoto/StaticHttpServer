using StaticFileServer.Http.Aggregates;
using StaticFileServer.Http.Exceptions;
using StaticFileServer.Http.Helper;
using System.Net;

namespace StaticFileServer.Http;

public class StaticFileServerHttp
{
    private HttpListener? _listener;

    private string _hostUrl = string.Empty;
    private string _hostDir = string.Empty;

    private string _notFoundResponseResource = "notFound.html";
    private string _errorResponseResource = "error.html";

    private bool _running;

    public StaticFileServerHttp(string hostUrl)
    {
        if (string.IsNullOrWhiteSpace(hostUrl))
        {
            throw new InvalidStaticFileServerInitializationException
            (
                "You must provide non null values for the constructor"
            );
        }

        _hostUrl = hostUrl;
        _hostDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "StatisFileServer-1.0", "static");

        Directory.CreateDirectory(_hostDir);
    }

    public StaticFileServerHttp(string hostUrl, string hostDir)
    {
        if (string.IsNullOrWhiteSpace(hostUrl) || string.IsNullOrWhiteSpace(hostDir))
        {
            throw new InvalidStaticFileServerInitializationException
            (
                "You must provide non null values for the constructor"
            );
        }

        _hostUrl = hostUrl;

        if (OperatingSystem.IsWindows())
        {
            _hostDir = hostDir.EndsWith('\\') ? hostDir : hostDir + "\\";
        }
        else
        {
            _hostDir = hostDir.EndsWith('/') ? hostDir : hostDir + "/";
        }
    }

    public async Task<int> RunAsync()
    {
        _listener = new();
        _listener.Prefixes.Add(_hostUrl);
        _listener.Start();

        Console.WriteLine($"Listenning on {_hostUrl}");

        _running = true;

        while (_running)
        {
            HttpListenerContext ctx = await _listener.GetContextAsync();

            await ProcessRequestAsync(ctx);
        }

        _listener.Close();

        return 0;
    }

    private async Task ProcessRequestAsync(HttpListenerContext ctx)
    {
        if (ctx?.Request?.Url is null)
        {
            throw new InvalidRequestContextException("The context or request or url cannot be null");
        }

        Console.WriteLine($"New request: {ctx.Request.HttpMethod} {ctx.Request.Url}");

        if (ctx.Request.Url!.AbsolutePath.ToLower().Equals("/shutdown"))
        {
            await HandleShutdownAynsc(ctx);

            return;
        }

        await HandleFileResponseAsync(ctx);
    }

    private async Task HandleShutdownAynsc(HttpListenerContext ctx)
    {
        await Task.Run(() =>
        {
            _running = false;
            SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.NoContent, 0));

            ctx.Response.Close();
        });
    }

    private async Task HandleFileResponseAsync(HttpListenerContext ctx)
    {
        var requestedResource = ctx.Request.Url!.AbsolutePath.ToString().TrimStart('/');
        string path = Path.Combine(_hostDir, (string.IsNullOrWhiteSpace(requestedResource) ? "index.html" : requestedResource));

        if (!File.Exists(path))
        {
            Console.WriteLine($"Page not found! Searched path: {path}");

            using var fileStream = new FileStream(Path.Combine(_hostDir, _notFoundResponseResource), FileMode.Open);
            await StreamHelper.WriteIntoOutputStreamAsync(ctx.Response.OutputStream, fileStream);

            SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.NotFound, 0));

            ctx.Response.Close();

            return;
        }

        try
        {
            await SendOkResponseAsync(ctx, path);
        }
        catch (HttpListenerException)
        {
            SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.InternalServerError, 0));

            ctx.Response.Close();
        }
        catch (Exception)
        {
            SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.BadRequest, 0));

            ctx.Response.Close();
        }
    }

    private async Task SendOkResponseAsync(HttpListenerContext ctx, string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open);
        await StreamHelper.WriteIntoOutputStreamAsync(ctx.Response.OutputStream, fileStream);

        Console.WriteLine($"Ok request! Searched path: {path}");

        string exthension = FileFormatContentTypeDictionary.GetHtttpContentTypeFromFile(path);

        SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.OK, exthension, (int)fileStream.Length));

        ctx.Response.Close();
    }

    private void SetResponseInfo(HttpListenerContext ctx, HttpResponseInfo responseInfo)
    {
        ctx.Response.StatusCode = (int)responseInfo.StatusCode;

        if (!ctx.Response.SendChunked)
        {
            ctx.Response.ContentLength64 = responseInfo.Lenght;
        }

        ctx.Response.ContentEncoding = responseInfo.Encoding;

        if (!string.IsNullOrWhiteSpace(responseInfo.ContentType))
        {
            ctx.Response.ContentType = responseInfo.ContentType;
        }

        Console.WriteLine($"Responding: {ctx.Response.StatusCode}, {ctx.Response.ContentType}, {ctx.Response.ContentLength64}");
    }
}