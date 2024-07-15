using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Util;

public class StringTemplateBuilder
{
    private readonly Regex pattern;
    private readonly Regex nlPattern = new Regex(@"\r\n?|\n", RegexOptions.Compiled);

    public StringTemplateBuilder(string pattern = "@\\{(.+?)}", RegexOptions options = RegexOptions.Compiled)
        : this(new Regex(pattern, options))
    {
    }

    public StringTemplateBuilder(Regex pattern)
    {
        this.pattern = pattern;
    }

    public IEnumerable<StringTemplate> Build(AdditionalText content, TemplateOptions options, CancellationToken token)
    {
        string name = PascalCaseTranform.Transform(Path.GetFileNameWithoutExtension(content.Path));
        string sourceFromFile = content.GetText(token)!.ToString();

        foreach ((string Key, string Template) in new TemplateReader().ReadToEnd(new StringReader(sourceFromFile)))
        {
            string source = Template;
            int index = 0;
            StringBuilder builder = new StringBuilder();
            HashSet<string> args = new();

            source = nlPattern.Replace(source, "\\n");
            foreach (Match match in pattern.Matches(source).Cast<Match>())
            {
                string before = source.Substring(index, match.Index - index).Replace("\"", "\\\"");
                string key = match.Groups[1].Value;
                args.Add($"string {key}");
                if (index > 0)
                    builder.Append(" + ");

                builder.Append("\"");
                builder.Append(before);
                builder.Append("\" + ");
                builder.Append(key);

                index = match.Index + key.Length + 3;
            }
            string remainder = source.Substring(index).Replace("\"", "\\\"");
            if (index > 0)
                builder.Append(" + ");
            builder.Append("\"");
            builder.Append(remainder);
            builder.Append("\"");

            yield return new(options, name, Key, builder.ToString(), args.ToArray());
        }
    }
}