using System;
using System.ClientModel;
using System.Threading;
using Azure.AI.OpenAI;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenAI;
using OpenAI.Chat;

namespace SourceGit.Models
{
    public class OpenAIService : ObservableObject
    {
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Server
        {
            get => _server;
            set => SetProperty(ref _server, value);
        }

        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        public string Model
        {
            get => _model;
            set => SetProperty(ref _model, value);
        }

        public string AnalyzeDiffPrompt
        {
            get => _analyzeDiffPrompt;
            set => SetProperty(ref _analyzeDiffPrompt, value);
        }

        public string GenerateSubjectPrompt
        {
            get => _generateSubjectPrompt;
            set => SetProperty(ref _generateSubjectPrompt, value);
        }

        public OpenAIService()
        {
            AnalyzeDiffPrompt = """
                You are an expert developer specialist in creating commits.
                Provide a super concise one sentence overall changes summary of the user `git diff` output following strictly the next rules:
                - Do not use any code snippets, imports, file routes or bullets points.
                - Do not mention the route of file that has been change.
                - Write clear, concise, and descriptive messages that explain the MAIN GOAL made of the changes.
                - Use the present tense and active voice in the message, for example, "Fix bug" instead of "Fixed bug.".
                - Use the imperative mood, which gives the message a sense of command, e.g. "Add feature" instead of "Added feature".
                - Avoid using general terms like "update" or "change", be specific about what was updated or changed.
                - Avoid using terms like "The main goal of", just output directly the summary in plain text
                """;

            GenerateSubjectPrompt = """
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
        }

        public void Chat(string prompt, string question, CancellationToken cancellation, Action<string> onUpdate)
        {
            Uri server = new(Server);
            ApiKeyCredential key = new(ApiKey);
            ChatClient client = null;
            if (Server.Contains("openai.azure.com/", StringComparison.Ordinal))
            {
                var azure = new AzureOpenAIClient(server, key);
                client = azure.GetChatClient(Model);
            }
            else
            {
                var openai = new OpenAIClient(key, new() { Endpoint = server });
                client = openai.GetChatClient(Model);
            }

            try
            {
                var updates = client.CompleteChatStreaming([
                    new UserChatMessage(prompt),
                    new UserChatMessage(question),
                ], null, cancellation);

                foreach (var update in updates)
                {
                    if (update.ContentUpdate.Count > 0)
                        onUpdate.Invoke(update.ContentUpdate[0].Text);
                }
            }
            catch
            {
                if (!cancellation.IsCancellationRequested)
                    throw;
            }
        }

        private string _name;
        private string _server;
        private string _apiKey;
        private string _model;
        private string _analyzeDiffPrompt;
        private string _generateSubjectPrompt;
    }
}
