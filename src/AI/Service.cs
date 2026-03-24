using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;

namespace SourceGit.AI
{
    public class Service
    {
        public Service(Models.AIProvider ai)
        {
            _ai = ai;
        }

        public async Task GenerateCommitMessage(string repo, string changeList, Action<string> onUpdate, CancellationToken cancellation)
        {
            var key = _ai.ReadApiKeyFromEnv ? Environment.GetEnvironmentVariable(_ai.ApiKey) : _ai.ApiKey;
            var endPoint = new Uri(_ai.Server);
            var credential = new ApiKeyCredential(key);
            var client = _ai.Server.Contains("openai.azure.com/", StringComparison.Ordinal)
                ? new AzureOpenAIClient(endPoint, credential)
                : new OpenAIClient(credential, new() { Endpoint = endPoint });

            var chatClient = client.GetChatClient(_ai.Model);
            var options = new ChatCompletionOptions() { Tools = { ChatTools.Tool_GetDetailChangesInFile } };

            var userMessageBuilder = new StringBuilder();
            userMessageBuilder
                .AppendLine("Generate a commit message (follow the rule of conventional commit message) for given git repository.")
                .AppendLine("- Read all given changed files before generating. Do not skip any one file.")
                .Append("Reposiory path: ").AppendLine(repo.Quoted())
                .AppendLine("Changed files: ")
                .Append(changeList);

            var messages = new List<ChatMessage>() { new UserChatMessage(userMessageBuilder.ToString()) };

            do
            {
                var inProgress = false;
                var updates = chatClient.CompleteChatStreamingAsync(messages, options).WithCancellation(cancellation);
                var toolCalls = new ToolCallsBuilder();
                var contentBuilder = new StringBuilder();

                await foreach (var update in updates)
                {
                    foreach (var contentPart in update.ContentUpdate)
                        contentBuilder.Append(contentPart.Text);

                    foreach (var toolCall in update.ToolCallUpdates)
                        toolCalls.Append(toolCall);

                    switch (update.FinishReason)
                    {
                        case ChatFinishReason.Stop:
                            onUpdate?.Invoke(string.Empty);
                            onUpdate?.Invoke("[Assistant]:");
                            onUpdate?.Invoke(contentBuilder.ToString());
                            break;
                        case ChatFinishReason.Length:
                            throw new Exception("The response was cut off because it reached the maximum length. Consider increasing the max tokens limit.");
                        case ChatFinishReason.ToolCalls:
                            {
                                var calls = toolCalls.Build();
                                var assistantMessage = new AssistantChatMessage(calls);
                                if (contentBuilder.Length > 0)
                                    assistantMessage.Content.Add(ChatMessageContentPart.CreateTextPart(contentBuilder.ToString()));
                                messages.Add(assistantMessage);

                                foreach (var call in calls)
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

                }

                if (!inProgress)
                    break;
            } while (true);
        }

        private readonly Models.AIProvider _ai;
    }
}
