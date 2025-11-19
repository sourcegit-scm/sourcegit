using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
                Args = $"diff --no-color --no-ext-diff --diff-algorithm=minimal {opt}";
            }

            public async Task<Result> ReadAsync()
            {
                return await ReadToEndAsync().ConfigureAwait(false);
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

        public async Task ExecAsync()
        {
            try
            {
                _onResponse?.Invoke("Waiting for pre-file analyzing to completed...\n\n");

                var responseBuilder = new StringBuilder();
                var summaryBuilder = new StringBuilder();
                foreach (var change in _changes)
                {
                    if (_cancelToken.IsCancellationRequested)
                        return;

                    responseBuilder.Append("- ");
                    summaryBuilder.Append("- ");

                    var rs = await new GetDiffContent(_repo, new Models.DiffOption(change, false)).ReadAsync();
                    if (rs.IsSuccess)
                    {
                        await _service.ChatAsync(
                            _service.AnalyzeDiffPrompt,
                            $"Here is the `git diff` output: {rs.StdOut}",
                            _cancelToken,
                            update =>
                            {
                                responseBuilder.Append(update);
                                summaryBuilder.Append(update);

                                _onResponse?.Invoke($"Waiting for pre-file analyzing to completed...\n\n{responseBuilder}");
                            });
                    }

                    responseBuilder.AppendLine();
                    summaryBuilder.Append("(file: ").Append(change.Path).AppendLine(")");
                }

                if (_cancelToken.IsCancellationRequested)
                    return;

                var responseBody = responseBuilder.ToString();
                var subjectBuilder = new StringBuilder();
                await _service.ChatAsync(
                    _service.GenerateSubjectPrompt,
                    $"Here are the summaries changes:\n{summaryBuilder}",
                    _cancelToken,
                    update =>
                    {
                        subjectBuilder.Append(update);
                        _onResponse?.Invoke($"{subjectBuilder}\n\n{responseBody}");
                    });
            }
            catch (Exception e)
            {
                App.RaiseException(_repo, $"Failed to generate commit message: {e}");
            }
        }

        private Models.OpenAIService _service;
        private string _repo;
        private List<Models.Change> _changes;
        private CancellationToken _cancelToken;
        private Action<string> _onResponse;
    }
}
