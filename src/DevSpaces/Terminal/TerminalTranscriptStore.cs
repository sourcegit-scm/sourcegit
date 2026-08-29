using System;
using System.Collections.Generic;
using System.Text;

namespace SourceGit.DevSpaces.Terminal
{
    public sealed class TerminalTranscriptStore
    {
        public const int DefaultCapacity = 3000;
        public const int MaximumReadBytes = 64 * 1024;

        public TerminalTranscriptStore(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _capacity = capacity;
        }

        public long AppendOutput(string text)
        {
            return Append(TerminalEventKind.Output, text ?? string.Empty, null);
        }

        public long AppendExit(int exitCode)
        {
            return Append(TerminalEventKind.Exit, string.Empty, exitCode);
        }

        public TerminalReadResult Read(long? afterSequence = null, int maxBytes = MaximumReadBytes)
        {
            if (maxBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes));

            maxBytes = Math.Min(maxBytes, MaximumReadBytes);

            lock (_gate)
            {
                if (_events.Count == 0)
                    return new TerminalReadResult([], 0, afterSequence ?? 0, false);

                var oldestSequence = _events[0].Sequence;
                var truncated = afterSequence.HasValue && afterSequence.Value < oldestSequence - 1;
                var cursor = afterSequence ?? 0;
                var result = new List<DevSpaceTerminalEvent>();
                var usedBytes = 0;

                foreach (var item in _events)
                {
                    if (item.Sequence <= cursor)
                        continue;

                    var eventBytes = Encoding.UTF8.GetByteCount(item.Text);
                    if (usedBytes + eventBytes > maxBytes)
                    {
                        if (result.Count == 0)
                        {
                            result.Add(item with { Text = TruncateUtf8(item.Text, maxBytes) });
                            truncated = true;
                        }

                        break;
                    }

                    result.Add(item);
                    usedBytes += eventBytes;
                }

                var nextSequence = result.Count > 0 ? result[^1].Sequence : cursor;
                return new TerminalReadResult(result.ToArray(), oldestSequence, nextSequence, truncated);
            }
        }

        public TerminalReadResult Tail(int maxEvents = 200, int maxBytes = MaximumReadBytes)
        {
            if (maxEvents <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxEvents));
            if (maxBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes));

            maxBytes = Math.Min(maxBytes, MaximumReadBytes);

            lock (_gate)
            {
                if (_events.Count == 0)
                    return new TerminalReadResult([], 0, 0, false);

                var start = Math.Max(0, _events.Count - maxEvents);
                var result = new List<DevSpaceTerminalEvent>();
                var usedBytes = 0;
                var truncated = false;

                for (var i = start; i < _events.Count; i++)
                {
                    var item = _events[i];
                    var eventBytes = Encoding.UTF8.GetByteCount(item.Text);
                    if (usedBytes + eventBytes > maxBytes)
                    {
                        if (result.Count == 0)
                        {
                            result.Add(item with { Text = TruncateUtf8(item.Text, maxBytes) });
                            truncated = true;
                        }

                        break;
                    }

                    result.Add(item);
                    usedBytes += eventBytes;
                }

                var nextSequence = result.Count > 0 ? result[^1].Sequence : 0;
                return new TerminalReadResult(result.ToArray(), _events[0].Sequence, nextSequence, truncated);
            }
        }

        private long Append(TerminalEventKind kind, string text, int? exitCode)
        {
            lock (_gate)
            {
                var sequence = ++_nextSequence;
                _events.Add(new DevSpaceTerminalEvent(
                    sequence,
                    DateTimeOffset.UtcNow,
                    kind,
                    text,
                    exitCode));

                if (_events.Count > _capacity)
                    _events.RemoveAt(0);

                return sequence;
            }
        }

        private static string TruncateUtf8(string text, int maxBytes)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var charIndex = 0;
            var usedBytes = 0;
            while (charIndex < text.Length)
            {
                var rune = Rune.GetRuneAt(text, charIndex);
                var runeBytes = rune.Utf8SequenceLength;
                if (usedBytes + runeBytes > maxBytes)
                    break;

                usedBytes += runeBytes;
                charIndex += rune.Utf16SequenceLength;
            }

            return charIndex == text.Length ? text : text[..charIndex];
        }

        private readonly object _gate = new();
        private readonly List<DevSpaceTerminalEvent> _events = [];
        private readonly int _capacity;
        private long _nextSequence;
    }
}
