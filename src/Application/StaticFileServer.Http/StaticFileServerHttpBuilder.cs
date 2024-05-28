namespace StaticFileServer.Http;

public class StaticFileServerHttpBuilder
{
    private string? _hostDir;
    private string? _hostUrl;

    public StaticFileServerHttpBuilder AddHostDir(string hostDir)
    {
        _hostDir = hostDir;

        return this;
    }

    public StaticFileServerHttpBuilder AddHostUrl(string hostUrl)
    {
        _hostUrl = hostUrl.EndsWith('/') ? hostUrl : hostUrl + "/";

        return this;
    }

    public StaticFileServerHttp Build()
    {
        if (string.IsNullOrWhiteSpace(_hostUrl))
        {
            //With the http://+:8080/ url I can send stuff as localhost and 127.0.0.1
            _hostUrl = "http://+:8080/";
        }

        if (string.IsNullOrWhiteSpace(_hostDir))
        {
            _hostDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "StatisFileServer-1.0", "static");
        }

        return new StaticFileServerHttp(_hostUrl!, _hostDir!);
    }
}