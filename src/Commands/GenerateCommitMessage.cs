using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

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

        public GenerateCommitMessage(string repo, List<Models.Change> changes, CancellationToken cancelToken, Action<string> onProgress)
        {
            _repo = repo;
            _changes = changes;
            _cancelToken = cancelToken;
            _onProgress = onProgress;
        }

        public string Result()
        {
            try
            {
                var summarybuilder = new StringBuilder();
                var bodyBuilder = new StringBuilder();
                foreach (var change in _changes)
                {
                    if (_cancelToken.IsCancellationRequested)
                        return "";

                    _onProgress?.Invoke($"Analyzing {change.Path}...");

                    var summary = GenerateChangeSummary(change);
                    summarybuilder.Append("- ");
                    summarybuilder.Append(summary);
                    summarybuilder.Append("(file: ");
                    summarybuilder.Append(change.Path);
                    summarybuilder.Append(")");
                    summarybuilder.AppendLine();

                    bodyBuilder.Append("- ");
                    bodyBuilder.Append(summary);
                    bodyBuilder.AppendLine();
                }

                if (_cancelToken.IsCancellationRequested)
                    return "";

                _onProgress?.Invoke($"Generating commit message...");

                var body = bodyBuilder.ToString();
                var subject = GenerateSubject(summarybuilder.ToString());
                return string.Format("{0}\n\n{1}", subject, body);
            }
            catch (Exception e)
            {
                App.RaiseException(_repo, $"Failed to generate commit message: {e}");
                return "";
            }
        }

        private static string GetSelectedLanguagePrompt()
        {
            var selectedLanguage = Models.OpenAI.SelectedLanguage == "English"
                ? string.Empty
                : $"{Environment.NewLine}Always write in {Models.OpenAI.SelectedLanguage}";

            return selectedLanguage;
        }

        private string GenerateChangeSummary(Models.Change change)
        {
            var rs = new GetDiffContent(_repo, new Models.DiffOption(change, false)).ReadToEnd();
            var diff = rs.IsSuccess ? rs.StdOut : "unknown change";
            var selectedLanguagePrompt = GetSelectedLanguagePrompt();

            var rsp = Models.OpenAI.Chat(Models.OpenAI.AnalyzeDiffPrompt + selectedLanguagePrompt, $"Here is the `git diff` output: {diff}", _cancelToken);
            if (rsp != null && rsp.Choices.Count > 0)
                return rsp.Choices[0].Message.Content;

            return string.Empty;
        }

        private string GenerateSubject(string summary)
        {
            var selectedLanguagePrompt = GetSelectedLanguagePrompt();
            var rsp = Models.OpenAI.Chat(Models.OpenAI.GenerateSubjectPrompt + selectedLanguagePrompt, $"Here are the summaries changes:\n{summary}", _cancelToken);
            if (rsp != null && rsp.Choices.Count > 0)
                return rsp.Choices[0].Message.Content;

            return string.Empty;
        }

        private string _repo;
        private List<Models.Change> _changes;
        private CancellationToken _cancelToken;
        private Action<string> _onProgress;
    }
}
