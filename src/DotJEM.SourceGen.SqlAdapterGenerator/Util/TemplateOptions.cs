namespace DotJEM.SourceGen.SqlAdapterGenerator.Util;

public readonly record struct TemplateOptions(string Namespace, string Visibility, string ClassName)
{
    public static TemplateOptions operator |(TemplateOptions left, TemplateOptions right)
        => new(Select(left.Namespace , right.Namespace), Select(left.Visibility, right.Visibility), Select(left.ClassName, right.ClassName));

    private static string Select(string one, string other)
        => !string.IsNullOrWhiteSpace(one) ? one : other;
}