using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace DevBoard.AI
{
    public class Agent
    {
        public Agent(Service service)
        {
            _service = service;
        }

        public async Task GenerateCommitMessageAsync(string repo, string currentBranch, string changeList, string amendParent, Action<string> onUpdate, CancellationToken cancellation)
        {
            if (_service.IsLocalLlm)
            {
                await GenerateCommitMessageWithLocalLlmAsync(repo, currentBranch, changeList, amendParent, onUpdate, cancellation);
                return;
            }

            var chatClient = _service.GetChatClient();
            if (chatClient == null)
                throw new Exception("Failed to fetch available models from this service. Please check your configuration and try again.");

            var options = new ChatCompletionOptions() { Tools = { ChatTools.GetDetailChangesInFile } };
#pragma warning disable OPENAI001
            if (!_service.ReasoningEffortLevel.Equals(Options.IgnoredReasoningEffortLevel, StringComparison.OrdinalIgnoreCase))
                options.ReasoningEffortLevel = new ChatReasoningEffortLevel(_service.ReasoningEffortLevel);
#pragma warning restore OPENAI001

            var userMessageBuilder = BuildCommitMessagePrompt(repo, currentBranch, changeList);
            var messages = new List<ChatMessage>() { new UserChatMessage(userMessageBuilder.ToString()) };

            do
            {
                ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options, cancellation);
                var inProgress = false;

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        if (onUpdate != null)
                        {
                            onUpdate.Invoke(string.Empty);
                            onUpdate.Invoke("# Assistant");
                            if (completion.Content.Count > 0)
                            {
                                var text = completion.Content[0].Text.ReplaceLineEndings("\n").Trim();
                                var start = 0;
                                var len = text.Length;
                                if (text.StartsWith("```", StringComparison.Ordinal))
                                {
                                    var idx = text.IndexOf('\n') + 1;
                                    start += idx;
                                    len -= idx;
                                }

                                if (text.EndsWith("\n```", StringComparison.Ordinal))
                                    len -= 4;

                                if (len > 0)
                                    onUpdate.Invoke(text.Substring(start, len));
                                else
                                    onUpdate.Invoke("[No content was generated.]");
                            }
                            else
                            {
                                onUpdate.Invoke("[No content was generated.]");
                            }

                            onUpdate.Invoke(string.Empty);
                            onUpdate.Invoke("# Token Usage");
                            onUpdate.Invoke($"Total: {completion.Usage.TotalTokenCount}. Input: {completion.Usage.InputTokenCount}. Output: {completion.Usage.OutputTokenCount}");
                        }
                        break;
                    case ChatFinishReason.Length:
                        throw new Exception("The response was cut off because it reached the maximum length. Consider increasing the max tokens limit.");
                    case ChatFinishReason.ToolCalls:
                        {
                            var message = new AssistantChatMessage(completion);
#pragma warning disable SCME0001
                            var hasReasoningContent = completion.Patch.TryGetValue("$.choices[0].message.reasoning_content"u8, out string reasoning);
                            if (hasReasoningContent)
                            {
                                if (string.IsNullOrEmpty(reasoning))
                                    message.Patch.Set("$.reasoning_content"u8, BinaryData.FromString("\"\""));
                                else
                                    message.Patch.Set("$.reasoning_content"u8, reasoning);
                            }
#pragma warning restore SCME0001
                            messages.Add(message);

                            foreach (var call in completion.ToolCalls)
                            {
                                var result = await ChatTools.ProcessAsync(call, repo, amendParent, onUpdate);
                                messages.Add(result);
                            }

                            inProgress = true;
                            break;
                        }
                    case ChatFinishReason.ContentFilter:
                        throw new Exception("Omitted content due to a content filter flag");
                    default:
                        break;
                }

                if (!inProgress)
                    break;
            } while (true);
        }

        private async Task GenerateCommitMessageWithLocalLlmAsync(string repo, string currentBranch, string changeList, string amendParent, Action<string> onUpdate, CancellationToken cancellation)
        {
            var prompt = BuildCommitMessagePrompt(repo, currentBranch, changeList);
            await AppendChangedFileDetailsAsync(prompt, repo, changeList, amendParent, onUpdate, cancellation);

            onUpdate?.Invoke(string.Empty);
            onUpdate?.Invoke("# Assistant");
            await _service.StreamLocalLlmAsync(prompt.ToString(), onUpdate, cancellation);
        }

        private StringBuilder BuildCommitMessagePrompt(string repo, string currentBranch, string changeList)
        {
            var builder = new StringBuilder();
            builder
                .AppendLine("Generate a commit message (follow the rule of conventional commit message) for given git repository.")
                .AppendLine("- Read all given changed files before generating. Only binary files (such as images, audios ...) can be skipped.")
                .AppendLine("- Output the conventional commit message (with detail changes in list) directly. Do not explain your output nor introduce your answer.")
                .AppendLine(_service.AdditionalPrompt)
                .Append("Repository path: ").AppendLine(repo.Quoted())
                .Append("Current branch: ").AppendLine(currentBranch.Quoted())
                .AppendLine("Changed files ('A' means added, 'M' means modified, 'D' means deleted, 'T' means type changed, 'R' means renamed, 'C' means copied): ")
                .AppendLine(changeList);
            return builder;
        }

        private static async Task AppendChangedFileDetailsAsync(StringBuilder prompt, string repo, string changeList, string amendParent, Action<string> onUpdate, CancellationToken cancellation)
        {
            prompt.AppendLine().AppendLine("Detailed changes:");
            foreach (var rawLine in changeList.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                cancellation.ThrowIfCancellationRequested();
                if (!TryParseChangedFile(rawLine, out var file, out var originalFile))
                    continue;

                try
                {
                    onUpdate?.Invoke($"Read changes in file: {file}");
                    var result = await new Commands.GetFileChangeForAI(repo, file, originalFile, amendParent).ReadAsync();
                    if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StdOut))
                        continue;

                    prompt.Append("--- ").Append(file).AppendLine(" ---");
                    prompt.AppendLine(result.StdOut);
                }
                catch
                {
                    // A binary, deleted, or otherwise unreadable file can be skipped.
                }
            }
        }

        private static bool TryParseChangedFile(string line, out string file, out string originalFile)
        {
            file = string.Empty;
            originalFile = string.Empty;

            var tabParts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tabParts.Length >= 2)
            {
                file = tabParts[^1].Trim('"');
                if (tabParts.Length >= 3 && (tabParts[0].StartsWith('R') || tabParts[0].StartsWith('C')))
                    originalFile = tabParts[^2].Trim('"');
                return !string.IsNullOrWhiteSpace(file);
            }

            if (line.Length < 3)
                return false;

            var firstSpace = line.IndexOf(' ');
            if (firstSpace < 0 || firstSpace == line.Length - 1)
                return false;

            file = line[(firstSpace + 1)..].Trim().Trim('"');
            var arrow = file.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow > 0)
            {
                originalFile = file[..arrow].Trim().Trim('"');
                file = file[(arrow + 4)..].Trim().Trim('"');
            }

            return !string.IsNullOrWhiteSpace(file);
        }

        private readonly Service _service;
    }
}
