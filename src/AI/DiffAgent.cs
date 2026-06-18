using System;
using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace SourceGit.AI
{
    public class DiffAgent
    {
        public async Task<string> AnalyzeAsync(Service service, string prompt, CancellationToken cancellationToken)
        {
            var chatClient = service.GetChatClient();
            if (chatClient == null)
                throw new InvalidOperationException("AI service is not configured correctly. Please check your configuration.");

            var messages = new ChatMessage[]
            {
                new UserChatMessage(prompt),
            };

            var options = new ChatCompletionOptions();
            ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
            if (completion.FinishReason == ChatFinishReason.Stop)
            {
                if (completion.Content.Count > 0)
                {
                    var text = completion.Content[0].Text.ReplaceLineEndings("\n").Trim();
                    return text;
                }
                return "[No content was generated.]";
            }

            if (completion.FinishReason == ChatFinishReason.Length)
                throw new InvalidOperationException("The response was cut off because it reached the maximum length. Consider increasing the max tokens limit.");

            if (completion.FinishReason == ChatFinishReason.ContentFilter)
                throw new InvalidOperationException("Omitted content due to a content filter flag.");

            return string.Empty;
        }

        public async Task<string> AnalyzeStreamingAsync(Service service, string prompt, Action<string> onChunk, CancellationToken cancellationToken)
        {
            var chatClient = service.GetChatClient();
            if (chatClient == null)
                throw new InvalidOperationException("AI service is not configured correctly. Please check your configuration.");

            var messages = new ChatMessage[]
            {
                new UserChatMessage(prompt),
            };

            var options = new ChatCompletionOptions();
            var fullText = string.Empty;

            try
            {
                var asyncUpdates = chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken);
                await foreach (var update in asyncUpdates)
                {
                    if (update.FinishReason == ChatFinishReason.Length)
                        throw new InvalidOperationException("The response was cut off because it reached the maximum length. Consider increasing the max tokens limit.");

                    if (update.FinishReason == ChatFinishReason.ContentFilter)
                        throw new InvalidOperationException("Omitted content due to a content filter flag.");

                    foreach (var content in update.ContentUpdate)
                    {
                        if (content.Kind == ChatMessageContentPartKind.Text && !string.IsNullOrEmpty(content.Text))
                        {
                            fullText += content.Text;
                            onChunk?.Invoke(content.Text);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            return fullText.ReplaceLineEndings("\n").Trim();
        }
    }
}
