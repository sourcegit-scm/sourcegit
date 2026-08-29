using System.Linq;
using System.Text;

using SourceGit.DevSpaces.Terminal;
using Xunit;

namespace SourceGit.Tests;

public class TerminalTranscriptStoreTests
{
    [Fact]
    public void Append_assigns_monotonic_sequence_numbers()
    {
        var store = new TerminalTranscriptStore(10);

        var first = store.AppendOutput("one\n");
        var second = store.AppendOutput("two\n");

        Assert.Equal(1, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public void Read_returns_only_events_after_cursor()
    {
        var store = new TerminalTranscriptStore(10);
        var first = store.AppendOutput("one\n");
        store.AppendOutput("two\n");

        var result = store.Read(first);

        var item = Assert.Single(result.Events);
        Assert.Equal("two\n", item.Text);
        Assert.Equal(2, item.Sequence);
        Assert.False(result.Truncated);
        Assert.Equal(2, result.NextSequence);
    }

    [Fact]
    public void Read_reports_truncation_for_stale_cursor()
    {
        var store = new TerminalTranscriptStore(2);
        store.AppendOutput("one\n");
        store.AppendOutput("two\n");
        store.AppendOutput("three\n");

        var result = store.Read(0);

        Assert.True(result.Truncated);
        Assert.Equal(2, result.OldestSequence);
        Assert.Equal(["two\n", "three\n"], result.Events.Select(x => x.Text));
    }

    [Fact]
    public void Tail_returns_only_requested_recent_events()
    {
        var store = new TerminalTranscriptStore(10);
        store.AppendOutput("one\n");
        store.AppendOutput("two\n");
        store.AppendOutput("three\n");

        var result = store.Tail(2);

        Assert.Equal(["two\n", "three\n"], result.Events.Select(x => x.Text));
        Assert.Equal(3, result.NextSequence);
    }

    [Fact]
    public void Read_honors_utf8_byte_limit_without_splitting_event_text()
    {
        var store = new TerminalTranscriptStore(10);
        store.AppendOutput("😀");
        store.AppendOutput("ok");

        var result = store.Read(maxBytes: Encoding.UTF8.GetByteCount("😀"));

        var item = Assert.Single(result.Events);
        Assert.Equal("😀", item.Text);
        Assert.Equal(Encoding.UTF8.GetByteCount("😀"), Encoding.UTF8.GetByteCount(item.Text));
        Assert.Equal(1, result.NextSequence);
    }

    [Fact]
    public void AppendExit_records_exit_event_and_code()
    {
        var store = new TerminalTranscriptStore(10);

        store.AppendExit(17);
        var result = store.Read();

        var item = Assert.Single(result.Events);
        Assert.Equal(TerminalEventKind.Exit, item.Kind);
        Assert.Equal(17, item.ExitCode);
        Assert.Equal(string.Empty, item.Text);
    }

    [Fact]
    public void Independent_reads_do_not_consume_events()
    {
        var store = new TerminalTranscriptStore(10);
        store.AppendOutput("hello");

        var first = store.Read();
        var second = store.Read();

        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.NextSequence, second.NextSequence);
    }
}
