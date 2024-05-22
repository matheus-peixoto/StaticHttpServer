using StaticFileServer.Http;

Console.WriteLine("Entry point has been called!");

Dictionary<string, Action<StaticFileServerHttpBuilder, string>> configurer = new()
{
    {
        "-d",
        (StaticFileServerHttpBuilder builder, string argVal) => builder.AddHostDir(argVal)
    },
    {
        "--directory",
        (StaticFileServerHttpBuilder builder, string argVal) => builder.AddHostDir(argVal)
    },
    {
        "-u",
        (StaticFileServerHttpBuilder builder, string argVal) => builder.AddHostUrl(argVal)
    },
    {
        "-url",
        (StaticFileServerHttpBuilder builder, string argVal) => builder.AddHostUrl(argVal)
    },
};

StaticFileServerHttpBuilder builder = new();
for (int i = 0; i < args.Length; i++)
{
    if (i == args.Length - 1) continue;

    if (configurer.ContainsKey(args[i].ToLower()))
    {
        configurer[args[i]](builder, args[i + 1]);
    }
}

StaticFileServerHttp server = builder.Build();
var result = await server.RunAsync();

Console.WriteLine($"Ended with code {result}, press any key to contiune");
Console.ReadKey();