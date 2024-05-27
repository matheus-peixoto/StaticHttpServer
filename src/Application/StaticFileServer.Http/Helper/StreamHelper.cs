namespace StaticFileServer.Http.Helper;

public static class StreamHelper
{
    public static void WriteIntoOutputStream(Stream outputStream, Stream inputStream)
    {
        //10 MiB max buffer size
        const int maxBufferByteSize = 1024 * 1024 * 10;
        byte[] buffer = new byte[maxBufferByteSize];

        int bytesToRead = (int)inputStream.Length;
        int totalReadedBytes = 0;
        while (bytesToRead > totalReadedBytes)
        {
            int startWriteIntoBufferFromIndex = 0;
            int readedBytes = inputStream.Read(buffer, startWriteIntoBufferFromIndex, maxBufferByteSize);

            int startReadIntoBufferFromIndex = 0;
            outputStream.Write(buffer, startReadIntoBufferFromIndex, readedBytes);
            totalReadedBytes += readedBytes;
        }
    }
}