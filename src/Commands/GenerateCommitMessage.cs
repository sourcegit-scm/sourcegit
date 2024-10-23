﻿using System;
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
        private const string DEFAULT_SUMMARY_PROMPT = """
            You are an expert developer specialist in creating commits.
            Provide a super concise one sentence overall changes summary of the user `git diff` output following strictly the next rules:
            - Do not use any code snippets, imports, file routes or bullets points.
            - Do not mention the route of file that has been change.
            - Simply describe the MAIN GOAL of the changes.
            - Output directly the summary in plain text.
            """;

        private const string DEFAULT_SUBJECT_PROMPT = """
            You are an expert developer specialist in creating commits messages.
            Your only goal is to retrieve a single commit message.
            Based on the provided user changes, combine them in ONE SINGLE commit message retrieving the global idea, following strictly the next rules:
            - Assign the commit {type} according to the next conditions:
              feat: Only when adding a new feature.
              fix: When fixing a bug.
              docs: When updating documentation.
              style: When changing elements styles or design and/or making changes to the code style (formatting, missing semicolons, etc.) without changing the code logic.
              test: When adding or updating tests.
              chore: When making changes to the build process or auxiliary tools and libraries.
              revert: When undoing a previous commit.
              refactor: When restructuring code without changing its external behavior, or is any of the other refactor types.
            - Do not add any issues numeration, explain your output nor introduce your answer.
            - Output directly only one commit message in plain text with the next format: {type}: {commit_message}.
            - Be as concise as possible, keep the message under 50 characters.
            """;

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
                var summaries = new List<string>();
                foreach (var change in _changes)
                {
                    if (_cancelToken.IsCancellationRequested)
                        return "";

                    _onProgress?.Invoke($"Analyzing {change.Path}...");
                    var summary = GenerateChangeSummary(change);
                    summaries.Add(summary);
                }

                if (_cancelToken.IsCancellationRequested)
                    return "";

                _onProgress?.Invoke($"Generating commit message...");
                var builder = new StringBuilder();
                builder.Append(GenerateSubject(string.Join("", summaries)));
                builder.Append("\n");
                foreach (var summary in summaries)
                {
                    builder.Append("\n- ");
                    builder.Append(summary.Trim());
                }

                return builder.ToString();
            }
            catch (Exception e)
            {
                App.RaiseException(_repo, $"Failed to generate commit message: {e}");
                return "";
            }
        }

        private string GenerateChangeSummary(Models.Change change)
        {
            var rs = new GetDiffContent(_repo, new Models.DiffOption(change, false)).ReadToEnd();
            var diff = rs.IsSuccess ? rs.StdOut : "unknown change";

            var prompt = string.IsNullOrWhiteSpace(Models.OpenAI.SummaryPrompt)
                ? DEFAULT_SUMMARY_PROMPT
                : Models.OpenAI.SummaryPrompt;

            var rsp = Models.OpenAI.Chat(prompt, $"Here is the `git diff` output: {diff}", _cancelToken);

            if (rsp != null && rsp.Choices.Count > 0)
                return rsp.Choices[0].Message.Content;

            return string.Empty;
        }

        private string GenerateSubject(string summary)
        {
            var prompt = string.IsNullOrWhiteSpace(Models.OpenAI.SubjectPrompt)
                ? DEFAULT_SUBJECT_PROMPT
                : Models.OpenAI.SubjectPrompt;

            var rsp = Models.OpenAI.Chat(prompt, $"Here are the summaries changes: {summary}", _cancelToken);

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
