namespace s3kira.Utils;

public static class XmlStreamReader
{
    public static string ReadValue(Stream stream, string key)
    {
        Span<byte> buffer = stackalloc byte[512];
        var size = stream.Read(buffer);
        if (size == 0)
            return string.Empty;
        
        var index = 0;
        var sectionStarted = false;
        var expectedIndex = 0;
        
        while (index < size)
        {
            var nextChar = (char)buffer[index];
            index++;
            
            if (sectionStarted)
            {
                if (nextChar == key[expectedIndex])
                {
                    if (++expectedIndex == key.Length)
                    {
                        if ((char)buffer[index] == '>') 
                            return ReadStringValue(buffer[++index..]);
                        expectedIndex = 0;
                        sectionStarted = false;
                    }
                }
                else
                {
                    sectionStarted = false;
                }
                
                continue;
            }

            if (nextChar == '<') sectionStarted = true;
        }

        return string.Empty;
    }

    private static string ReadStringValue(Span<byte> buffer)
    {
        var builder = new ValueStringBuilder(stackalloc char[128]);
        var index = 0;
        while (index < buffer.Length && index < 128)
        {
            var nextChar = (char)buffer[index];
            if (nextChar == '<') 
                return builder.Flush();
            builder.Append(nextChar);
            index++;
        }
        return string.Empty;
    }
}