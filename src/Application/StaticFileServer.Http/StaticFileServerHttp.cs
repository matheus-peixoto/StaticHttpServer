using StaticFileServer.Http.Aggregates;
using StaticFileServer.Http.Exceptions;
using StaticFileServer.Http.Helper;
using System.Net;

namespace StaticFileServer.Http;

public class StaticFileServerHttp
{
    private const int ThreadPoolSize = 100;

    private readonly AutoResetEvent _queueNotifier;
    private readonly Mutex _mutex;
    private readonly HttpListener _listener;

    private readonly string _hostUrl = string.Empty;
    private readonly string _hostDir = string.Empty;

    private readonly string _notFoundResponseResource = "notFound.html";
    private readonly string _errorResponseResource = "error.html";

    private readonly Thread[] _threadPool;

    private readonly Queue<HttpListenerContext> _contextQueue;

    private long _isMutexOnLock;
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

        _queueNotifier = new AutoResetEvent(false);
        _mutex = new();
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

        _queueNotifier = new AutoResetEvent(false);
        _mutex = new();
        _threadPool = new Thread[ThreadPoolSize];
        _contextQueue = new();
        _listener = new();
    }

    private void StartThreads()
    {
        for (int i = 0; i < ThreadPoolSize; i++)
        {
            _threadPool[i] = new Thread(ListenOnQueue);

            var thread = _threadPool[i];
            thread.Name = $"Thread{i + 1}";
            thread.Start();
        }
    }

    private void ReleaseThreads()
    {
        for (int i = 0; i < ThreadPoolSize; i++)
        {
            _queueNotifier.Set();
        }
    }

    private void ListenOnQueue()
    {
        Guid executionContextId = Guid.NewGuid();
        while (Interlocked.Read(ref _running) == 1)
        {
            _queueNotifier.WaitOne();

            if (Interlocked.Read(ref _running) == 0) continue;

            MutexRequest(executionContextId);

            var ctx = _contextQueue.Dequeue();

            MutexRelease(executionContextId);

            if (ctx == null) continue;

            Console.WriteLine($"{Thread.CurrentThread.Name} will proccess request of context {executionContextId}");
            ProcessRequest(ctx);
            Console.WriteLine($"{Thread.CurrentThread.Name} proccessed request of context {executionContextId}");
        }

        Console.WriteLine($"{Thread.CurrentThread.Name} has concluded");
    }

    public int Run()
    {
        Guid executionContextId = Guid.NewGuid();

        _listener!.Prefixes.Add(_hostUrl);
        _listener!.Start();

        Console.WriteLine($"Listenning on {_hostUrl}");

        _running = 1;

        StartThreads();

        while (Interlocked.Read(ref _running) == 1)
        {
            ListenForNewRequest(_listener, executionContextId);
        }

        Dispose();

        return 0;
    }

    private void ListenForNewRequest(HttpListener listener, Guid executionContextId)
    {
        try
        {
            HttpListenerContext ctx = listener.GetContext();

            MutexRequest(executionContextId);

            Console.WriteLine($"New request: {ctx.Request.HttpMethod} {ctx.Request.Url}");

            _contextQueue.Enqueue(ctx);

            MutexRelease(executionContextId);

            _queueNotifier.Set();
        }
        catch (HttpListenerException ex)
        {
            const int stopListennerErrCode = 995;

            if (ex.ErrorCode != stopListennerErrCode)
            {
                throw;
            }
        }
        catch (Exception)
        {
            if (Interlocked.Read(ref _isMutexOnLock) == 1)
            {
                MutexRelease(executionContextId);
            }

            throw;
        }
    }

    private void ProcessRequest(HttpListenerContext ctx)
    {
        if (ctx?.Request?.Url is null)
        {
            throw new InvalidRequestContextException("The context or request or url cannot be null");
        }

        if (ctx.Request.Url!.AbsolutePath.ToLower().Equals("/shutdown"))
        {
            HandleShutdown(ctx);

            return;
        }

        HandleFileResponse(ctx);
    }

    private void HandleShutdown(HttpListenerContext ctx)
    {
        Interlocked.Exchange(ref _running, 0);

        SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.NoContent, 0));

        ctx.Response.Close();

        _listener.Stop();

        ReleaseThreads();
    }

    private void HandleFileResponse(HttpListenerContext ctx)
    {
        var requestedResource = ctx.Request.Url!.AbsolutePath.ToString().TrimStart('/');
        string path = Path.Combine(_hostDir, (string.IsNullOrWhiteSpace(requestedResource) ? "index.html" : requestedResource));

        if (!File.Exists(path))
        {
            Console.WriteLine($"Page not found! Searched path: {path}");

            using var fileStream = new FileStream(Path.Combine(_hostDir, _notFoundResponseResource), FileMode.Open, FileAccess.Read, FileShare.Read);
            StreamHelper.WriteIntoOutputStream(ctx.Response.OutputStream, fileStream);

            SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.NotFound, 0));

            ctx.Response.Close();

            return;
        }

        try
        {
            SendOkResponse(ctx, path);
        }
        catch (HttpListenerException ex)
        {
            Console.WriteLine($"Receive an {ex.GetType()} exception: {ex.Message}");

            SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.InternalServerError, 0));

            ctx.Response.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Receive an {ex.GetType()} exception: {ex.Message}");

            SetResponseInfo(ctx, new HttpResponseInfo(HttpStatusCode.BadRequest, 0));

            ctx.Response.Close();
        }
    }

    private void SendOkResponse(HttpListenerContext ctx, string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        StreamHelper.WriteIntoOutputStream(ctx.Response.OutputStream, fileStream);

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

    private void MutexRequest(Guid executionContextId)
    {
        _mutex.WaitOne();

        Interlocked.Exchange(ref _isMutexOnLock, 1);

        LogMutexRequest(executionContextId);
    }

    private void MutexRelease(Guid executionContextId)
    {
        _mutex.ReleaseMutex();

        Interlocked.Exchange(ref _isMutexOnLock, 0);

        LogMutexRelease(executionContextId);
    }

    private void LogMutexRequest(Guid executionContextId) =>
        Console.WriteLine($"{Thread.CurrentThread.Name} is requesting mutex - Execution Context: {executionContextId}");

    private void LogMutexRelease(Guid executionContextId) =>
        Console.WriteLine($"{Thread.CurrentThread.Name} released mutex - Execution Context: {executionContextId}");

    private void Dispose()
    {
        _listener.Close();
        _mutex.Dispose();
        _queueNotifier.Dispose();
    }
}