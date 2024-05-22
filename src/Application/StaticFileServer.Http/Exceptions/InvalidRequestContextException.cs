namespace StaticFileServer.Http.Exceptions;

public class InvalidRequestContextException : Exception
{
    public InvalidRequestContextException(string msg) : base(msg) { }
}