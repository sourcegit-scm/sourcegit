using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace SourceGit.AI
{
    public class Agent
    {
        public Agent(Service service)
        {
            _service = service;
        }

        public async Task GenerateCommitMessageAsync(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation)
        {
            var chatClient = _service.GetChatClient();
            if (chatClient == null)
                throw new Exception("Failed to fetch available models from this service. Please check your configuration and try again.");

            var options = new ChatCompletionOptions() { Tools = { ChatTools.GetDetailChangesInFile } };
            var userMessageBuilder = new StringBuilder();
            userMessageBuilder
                .AppendLine("Generate a commit message (follow the rule of conventional commit message) for given git repository.")
                .AppendLine("- Read all given changed files before generating. Only binary files (such as images, audios ...) can be skipped.")
                .AppendLine("- Output the conventional commit message (with detail changes in list) directly. Do not explain your output nor introduce your answer.")
                .AppendLine(_service.AdditionalPrompt)
                .Append("Repository path: ").AppendLine(repo.Quoted())
                .AppendLine("Changed files ('A' means added, 'M' means modified, 'D' means deleted, 'T' means type changed, 'R' means renamed, 'C' means copied): ")
                .Append(changeList);

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
                                if (text.StartsWith("```\n", StringComparison.Ordinal))
                                {
                                    start += 4;
                                    len -= 4;
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
                                message.Patch.Set("$.reasoning_content"u8, reasoning);
#pragma warning restore SCME0001
                            messages.Add(message);

                            foreach (var call in completion.ToolCalls)
                            {
                                var result = await ChatTools.ProcessAsync(call, onUpdate);
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

        private readonly Service _service;
    }
}
