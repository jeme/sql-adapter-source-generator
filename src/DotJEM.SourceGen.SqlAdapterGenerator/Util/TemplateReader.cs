using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Util;

public class TemplateReader
{
    public IEnumerable<(string Key, string Template)> ReadToEnd(StringReader reader)
    {
        StringBuilder buffer = new();
        string section = "default";
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("--start"))
            {
                string[] parts = line.Split(':');
                if (parts.Length == 2)
                    section = parts[1].Trim();
                continue;
            }

            if (line.StartsWith("--end"))
            {
                string template = buffer.ToString();
                buffer.Clear();
                yield return (section, template);
                continue;
            }

            buffer.AppendLine(line);
        }

        if (buffer.Length > 0)
        {
            string template = buffer.ToString();
            yield return (section, template);
        }
    }
}