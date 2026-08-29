using System;

namespace DevBoard.DevSpaces.Terminal
{
    public sealed class TerminalTranscriptSink
    {
        public TerminalTranscriptSink(TerminalTranscriptStore transcript)
        {
            _transcript = transcript ?? throw new ArgumentNullException(nameof(transcript));
        }

        public void WriteOutput(string text)
        {
            if (!string.IsNullOrEmpty(text))
                _transcript.AppendOutput(text);
        }

        public void RecordExit(int exitCode)
        {
            _transcript.AppendExit(exitCode);
        }

        private readonly TerminalTranscriptStore _transcript;
    }
}
