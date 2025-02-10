using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

using Avalonia.Threading;

namespace SourceGit.Commands
{
    /// <summary>
    ///     A C# version of https://github.com/anjerodev/commitollama
    /// </summary>
    public class GenerateCommitMessage
    {
        public class GetDiffContent : Command
        {
            public GetDiffContent(string repo, Models.DiffOption opt)
            {
                WorkingDirectory = repo;
                Context = repo;
                Args = $"diff --diff-algorithm=minimal {opt}";
            }
        }

        public GenerateCommitMessage(Models.OpenAIService service, string repo, List<Models.Change> changes, CancellationToken cancelToken, Action<string> onResponse)
        {
            _service = service;
            _repo = repo;
            _changes = changes;
            _cancelToken = cancelToken;
            _onResponse = onResponse;
        }

        public void Exec()
        {
            try
            {
                var responseBuilder = new StringBuilder();
                var summaryBuilder = new StringBuilder();
                foreach (var change in _changes)
                {
                    if (_cancelToken.IsCancellationRequested)
                        return;

                    responseBuilder.Append("- ");
                    summaryBuilder.Append("- ");

                    var rs = new GetDiffContent(_repo, new Models.DiffOption(change, false)).ReadToEnd();
                    if (rs.IsSuccess)
                    {
                        var hasFirstValidChar = false;
                        var thinkingBuffer = new StringBuilder();
                        _service.Chat(
                            _service.AnalyzeDiffPrompt,
                            $"Here is the `git diff` output: {rs.StdOut}",
                            _cancelToken,
                            update =>
                                ProcessChatResponse(update, ref hasFirstValidChar, thinkingBuffer,
                                    (responseBuilder, text =>
                                        _onResponse?.Invoke(
                                            $"Waiting for pre-file analyzing to completed...\n\n{text}")),
                                    (summaryBuilder, null)));
                    }

                    responseBuilder.Append("\n");
                    summaryBuilder.Append("(file: ");
                    summaryBuilder.Append(change.Path);
                    summaryBuilder.Append(")\n");
                }

                if (_cancelToken.IsCancellationRequested)
                    return;

                var responseBody = responseBuilder.ToString();
                var subjectBuilder = new StringBuilder();
                var hasSubjectFirstValidChar = false;
                var subjectThinkingBuffer = new StringBuilder();
                _service.Chat(
                    _service.GenerateSubjectPrompt,
                    $"Here are the summaries changes:\n{summaryBuilder}",
                    _cancelToken,
                    update =>
                        ProcessChatResponse(update, ref hasSubjectFirstValidChar, subjectThinkingBuffer,
                            (subjectBuilder, text => _onResponse?.Invoke($"{text}\n\n{responseBody}"))));
            }
            catch (Exception e)
            {
                Dispatcher.UIThread.Post(() => App.RaiseException(_repo, $"Failed to generate commit message: {e}"));
            }
        }

        private void ProcessChatResponse(
            string update,
            ref bool hasFirstValidChar,
            StringBuilder thinkingBuffer,
            params (StringBuilder builder, Action<string> callback)[] outputs)
        {
            if (!hasFirstValidChar)
            {
                update = update.TrimStart();
                if (string.IsNullOrEmpty(update))
                    return;
                if (update.StartsWith("<", StringComparison.Ordinal))
                    thinkingBuffer.Append(update);
                hasFirstValidChar = true;
            }

            if (thinkingBuffer.Length > 0)
                thinkingBuffer.Append(update);

            if (thinkingBuffer.Length > 15)
            {
                var match = REG_COT.Match(thinkingBuffer.ToString());
                if (match.Success)
                {
                    update = REG_COT.Replace(thinkingBuffer.ToString(), "").TrimStart();
                    if (update.Length > 0)
                    {
                        foreach (var output in outputs)
                            output.builder.Append(update);
                        thinkingBuffer.Clear();
                    }
                    return;
                }

                match = REG_THINK_START.Match(thinkingBuffer.ToString());
                if (!match.Success)
                {
                    foreach (var output in outputs)
                        output.builder.Append(thinkingBuffer);
                    thinkingBuffer.Clear();
                    return;
                }
            }

            if (thinkingBuffer.Length == 0)
            {
                foreach (var output in outputs)
                {
                    output.builder.Append(update);
                    output.callback?.Invoke(output.builder.ToString());
                }
            }
        }

        private Models.OpenAIService _service;
        private string _repo;
        private List<Models.Change> _changes;
        private CancellationToken _cancelToken;
        private Action<string> _onResponse;

        private static readonly Regex REG_COT = new(@"^<(think|thought|thinking|thought_chain)>(.*?)</\1>", RegexOptions.Singleline);
        private static readonly Regex REG_THINK_START = new(@"^<(think|thought|thinking|thought_chain)>", RegexOptions.Singleline);
    }
}
