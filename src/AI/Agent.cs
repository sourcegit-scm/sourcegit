using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using OpenAI;
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
            var endPoint = new Uri(_service.Server);
            var client = _service.Server.Contains("openai.azure.com/", StringComparison.Ordinal)
                ? new AzureOpenAIClient(endPoint, _service.Credential)
                : new OpenAIClient(_service.Credential, new() { Endpoint = endPoint });

            var chatClient = client.GetChatClient(_service.Model);
            var options = new ChatCompletionOptions() { Tools = { ChatTools.GetDetailChangesInFile } };

            var userMessageBuilder = new StringBuilder();
            userMessageBuilder
                .AppendLine("Generate a commit message (follow the rule of conventional commit message) for given git repository.")
                .AppendLine("- Read all given changed files before generating. Do not skip any one file.")
                .AppendLine("- Output the conventional commit message (with detail changes in list) directly. Do not explain your output nor introduce your answer.")
                .AppendLine(string.IsNullOrEmpty(_service.AdditionalPrompt) ? string.Empty : _service.AdditionalPrompt)
                .Append("Reposiory path: ").AppendLine(repo.Quoted())
                .AppendLine("Changed files: ")
                .Append(changeList);

            var messages = new List<ChatMessage>() { new UserChatMessage(userMessageBuilder.ToString()) };

            do
            {
                ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options, cancellation);
                var inProgress = false;

                switch (completion.FinishReason)
                {
                    case ChatFinishReason.Stop:
                        onUpdate?.Invoke(string.Empty);
                        onUpdate?.Invoke("# Assistant");
                        if (completion.Content.Count > 0)
                            onUpdate?.Invoke(completion.Content[0].Text);
                        else
                            onUpdate?.Invoke("[No content was generated.]");

                        onUpdate?.Invoke(string.Empty);
                        onUpdate?.Invoke("# Token Usage");
                        onUpdate?.Invoke($"Total: {completion.Usage.TotalTokenCount}. Input: {completion.Usage.InputTokenCount}. Output: {completion.Usage.OutputTokenCount}");
                        break;
                    case ChatFinishReason.Length:
                        throw new Exception("The response was cut off because it reached the maximum length. Consider increasing the max tokens limit.");
                    case ChatFinishReason.ToolCalls:
                        {
                            messages.Add(new AssistantChatMessage(completion));

                            foreach (var call in completion.ToolCalls)
                            {
                                var result = await ChatTools.Process(call, onUpdate);
                                messages.Add(result);
                            }

                            inProgress = true;
                            break;
                        }
                    case ChatFinishReason.ContentFilter:
                        throw new Exception("Ommitted content due to a content filter flag");
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
