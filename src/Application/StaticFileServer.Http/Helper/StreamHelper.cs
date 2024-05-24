namespace StaticFileServer.Http.Helper;

public static class StreamHelper
{
    public static async Task WriteIntoOutputStreamAsync(Stream outputStream, Stream inputStream)
    {
        const int maxBufferByteSize = 1000;
        byte[] buffer = new byte[maxBufferByteSize];

        int bytesToRead = (int)inputStream.Length;
        int totalReadedBytes = 0;
        while (bytesToRead > totalReadedBytes)
        {
            int startWriteIntoBufferFromIndex = 0;
            int readedBytes = await inputStream.ReadAsync(buffer, startWriteIntoBufferFromIndex, maxBufferByteSize);

            int startReadIntoBufferFromIndex = 0;
            await outputStream.WriteAsync(buffer, startReadIntoBufferFromIndex, readedBytes);
            totalReadedBytes += readedBytes;
        }
    }
}