namespace DotJEM.SourceGen.SqlAdapterGenerator.Util;

public readonly record struct StringTemplate(TemplateOptions Options, string Name, string Key, string Source, string[] Args)
{
    public override string ToString()
    {
        return $$""""
                 namespace {{Options.Namespace}};

                 {{Options.Visibility}} static partial class {{Options.ClassName}}
                 {
                      public static string {{Name}}_{{Key}}({{string.Join(", ", Args)}})
                      {
                          return {{Source}};
                      }
                 }
                 """";
    }
}