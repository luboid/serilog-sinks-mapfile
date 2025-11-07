using Serilog.Parsing;
using System.Text;

namespace Serilog.Sinks.MapPattern;

internal static class Padding
{
    private static readonly char[] _paddingChars = Enumerable.Repeat(' ', 80).ToArray();

    public static void Apply(TextWriter output, string value, in Alignment? alignment)
    {
        if (!alignment.HasValue || value.Length >= alignment.Value.Width)
        {
            output.Write(value);
            return;
        }
        int num = alignment.Value.Width - value.Length;
        if (alignment.Value.Direction == AlignmentDirection.Left)
        {
            output.Write(value);
        }
        if (num <= _paddingChars.Length)
        {
            output.Write(_paddingChars, 0, num);
        }
        else
        {
            output.Write(new string(' ', num));
        }
        if (alignment.Value.Direction == AlignmentDirection.Right)
        {
            output.Write(value);
        }
    }

    public static void Apply(TextWriter output, StringBuilder value, in Alignment? alignment)
    {
        if (!alignment.HasValue || value.Length >= alignment.Value.Width)
        {
            output.Write(value);
            return;
        }
        int num = alignment.Value.Width - value.Length;
        if (alignment.Value.Direction == AlignmentDirection.Left)
        {
            output.Write(value);
        }
        if (num <= _paddingChars.Length)
        {
            output.Write(_paddingChars, 0, num);
        }
        else
        {
            output.Write(new string(' ', num));
        }
        if (alignment.Value.Direction == AlignmentDirection.Right)
        {
            output.Write(value);
        }
    }
}