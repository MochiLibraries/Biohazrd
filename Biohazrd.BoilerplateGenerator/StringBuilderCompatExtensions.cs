namespace System.Text;

internal static class StringBuilderCompatExtensions
{
    public static void WriteLine(this StringBuilder sb)
        => sb.AppendLine();

    public static void WriteLine(this StringBuilder sb, string value)
        => sb.AppendLine(value);

    public static void Write(this StringBuilder sb, string value)
        => sb.Append(value);

    public static void Flush(this StringBuilder sb)
    { }
}
