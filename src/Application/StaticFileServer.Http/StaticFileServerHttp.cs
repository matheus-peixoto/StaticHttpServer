﻿using StaticFileServer.Http.Aggregates;
using StaticFileServer.Http.Exceptions;
using StaticFileServer.Http.Helper;
using System.Net;

namespace StaticFileServer.Http;

public class StaticFileServerHttp
{
    private const int ThreadPoolSize = 20;

    private readonly HttpListener _listener;

    private readonly string _hostUrl = string.Empty;
    private readonly string _hostDir = string.Empty;

    private readonly string _notFoundResponseResource = "notFound.html";
    private readonly string _errorResponseResource = "error.html";

    private readonly Thread[] _threadPool;

    private readonly Queue<HttpListenerContext> _contextQueue;

    private long _running;

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

        _threadPool = new Thread[ThreadPoolSize];
        _contextQueue = new();
        _listener = new();
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

        _threadPool = new Thread[ThreadPoolSize];
        _contextQueue = new();
        _listener = new();
    }

    public async Task ListenOnQueueAsync()
    {
        Guid executionContext = Guid.NewGuid();
        while (Interlocked.Read(ref _running) == 1)
        {
            if (_contextQueue.Count == 0)
            {
                continue;
            };

            var ctx = _contextQueue.Dequeue();

            await Console.Out.WriteLineAsync($"{Thread.CurrentThread.Name} will proccess request of context {executionContext}");
            await ProcessRequestAsync(ctx);
            await Console.Out.WriteLineAsync($"{Thread.CurrentThread.Name} proccessed request of context {executionContext}");
        }

        await Console.Out.WriteLineAsync($"{Thread.CurrentThread.Name} has concluded");
    }

    public void StartThreads()
    {
        for (int i = 0; i < ThreadPoolSize; i++)
        {
            _threadPool[i] = new Thread(async () => await ListenOnQueueAsync());

            var thread = _threadPool[i];
            thread.Name = $"Thread{i + 1}";
            thread.Start();
        }
    }

    public async Task<int> RunAsync()
    {
        Guid executionId = Guid.NewGuid();

        _listener!.Prefixes.Add(_hostUrl);
        _listener!.Start();

        Console.WriteLine($"Listenning on {_hostUrl}");

        _running = 1;

        StartThreads();

        while (Interlocked.Read(ref _running) == 1)
        {
            await ListenForNewRequestAsync(_listener, executionId);
        }

        _listener.Close();

        return 0;
    }

    private async Task ListenForNewRequestAsync(HttpListener listener, Guid executionId)
    {
        try
        {
            HttpListenerContext ctx = await listener.GetContextAsync();

            Console.WriteLine($"New request: {ctx.Request.HttpMethod} {ctx.Request.Url}");

            _contextQueue.Enqueue(ctx);
        }
        catch (HttpListenerException ex)
        {
            const int stopListennerErrCode = 995;

            if (ex.ErrorCode != stopListennerErrCode)
            {
                throw;
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext ctx)
    {
        if (ctx?.Request?.Url is null)
        {
            throw new InvalidRequestContextException("The context or request or url cannot be null");
        }

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
            Interlocked.Exchange(ref _running, 0);
            SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.NoContent, 0));

            ctx.Response.Close();

            _listener.Stop();
        });
    }

    private async Task HandleFileResponseAsync(HttpListenerContext ctx)
    {
        var requestedResource = ctx.Request.Url!.AbsolutePath.ToString().TrimStart('/');
        string path = Path.Combine(_hostDir, (string.IsNullOrWhiteSpace(requestedResource) ? "index.html" : requestedResource));

        if (!File.Exists(path))
        {
            Console.WriteLine($"Page not found! Searched path: {path}");

            using var fileStream = new FileStream(Path.Combine(_hostDir, _notFoundResponseResource), FileMode.Open, FileAccess.Read, FileShare.Read);
            await StreamHelper.WriteIntoOutputStreamAsync(ctx.Response.OutputStream, fileStream);

            SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.NotFound, 0));

            ctx.Response.Close();

            return;
        }

        try
        {
            await SendOkResponseAsync(ctx, path);
        }
        catch (HttpListenerException ex)
        {
            await Console.Out.WriteLineAsync($"Receive an {ex.GetType()} exception: {ex.Message}");

            SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.InternalServerError, 0));

            ctx.Response.Close();
        }
        catch (Exception ex)
        {
            await Console.Out.WriteLineAsync($"Receive an {ex.GetType()} exception: {ex.Message}");

            SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.BadRequest, 0));

            ctx.Response.Close();
        }
    }

    private async Task SendOkResponseAsync(HttpListenerContext ctx, string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
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