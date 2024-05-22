namespace StaticFileServer.Http.Exceptions;

public class InvalidStaticFileServerInitializationException : Exception
{
    public InvalidStaticFileServerInitializationException(string msg) : base(msg) { }
}