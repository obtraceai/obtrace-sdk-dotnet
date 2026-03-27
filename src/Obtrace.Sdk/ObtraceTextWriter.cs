using System.Text;

namespace Obtrace.Sdk;

internal sealed class ObtraceTextWriter : TextWriter
{
    private readonly TextWriter _inner;
    private readonly ObtraceClient _client;
    private readonly string _level;

    public override Encoding Encoding => _inner.Encoding;

    public ObtraceTextWriter(TextWriter inner, ObtraceClient client, string level)
    {
        _inner = inner;
        _client = client;
        _level = level;
    }

    public TextWriter Inner => _inner;

    public override void WriteLine(string? value)
    {
        _inner.WriteLine(value);
        if (value is not null && !value.StartsWith("[obtrace"))
            _client.Log(_level, value);
    }

    public override void Write(string? value)
    {
        _inner.Write(value);
    }

    public override void WriteLine()
    {
        _inner.WriteLine();
    }

    public override void Flush() => _inner.Flush();

    public override Task FlushAsync() => _inner.FlushAsync();
}
