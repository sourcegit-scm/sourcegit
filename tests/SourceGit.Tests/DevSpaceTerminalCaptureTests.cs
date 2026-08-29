using SourceGit.DevSpaces.Terminal;
using Xunit;

namespace SourceGit.Tests;

public class DevSpaceTerminalCaptureTests
{
    [Fact]
    public void Sink_writes_terminal_output_to_transcript()
    {
        var store = new TerminalTranscriptStore(10);
        var sink = new TerminalTranscriptSink(store);

        sink.WriteOutput("build output\r\n");

        var result = store.Read();
        var item = Assert.Single(result.Events);
        Assert.Equal(TerminalEventKind.Output, item.Kind);
        Assert.Equal("build output\r\n", item.Text);
    }

    [Fact]
    public void Sink_records_terminal_exit_once_per_call()
    {
        var store = new TerminalTranscriptStore(10);
        var sink = new TerminalTranscriptSink(store);

        sink.RecordExit(7);

        var result = store.Read();
        var item = Assert.Single(result.Events);
        Assert.Equal(TerminalEventKind.Exit, item.Kind);
        Assert.Equal(7, item.ExitCode);
    }
}
