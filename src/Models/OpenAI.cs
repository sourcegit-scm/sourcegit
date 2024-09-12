﻿using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SourceGit.Models
{
    public class OpenAIChatMessage
    {
        [JsonPropertyName("role")]
        public string Role
        {
            get;
            set;
        }

        [JsonPropertyName("content")]
        public string Content
        {
            get;
            set;
        }
    }

    public class OpenAIChatChoice
    {
        [JsonPropertyName("index")]
        public int Index
        {
            get;
            set;
        }

        [JsonPropertyName("message")]
        public OpenAIChatMessage Message
        {
            get;
            set;
        }
    }

    public class OpenAIChatResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAIChatChoice> Choices
        {
            get;
            set;
        } = [];
    }

    public class OpenAIChatRequest
    {
        [JsonPropertyName("model")]
        public string Model
        {
            get;
            set;
        }

        [JsonPropertyName("messages")]
        public List<OpenAIChatMessage> Messages
        {
            get;
            set;
        } = [];

        public void AddMessage(string role, string content)
        {
            Messages.Add(new OpenAIChatMessage { Role = role, Content = content });
        }
    }

    public static class OpenAI
    {
        public static string Server
        {
            get;
            set;
        }

        public static string ApiKey
        {
            get;
            set;
        }

        public static string Model
        {
            get;
            set;
        }

        public static bool IsValid
        {
            get => !string.IsNullOrEmpty(Server) && !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(Model);
        }

        public static OpenAIChatResponse Chat(string prompt, string question)
        {
            var chat = new OpenAIChatRequest() { Model = Model };
            chat.AddMessage("system", prompt);
            chat.AddMessage("user", question);

            var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");

            var req = new StringContent(JsonSerializer.Serialize(chat, JsonCodeGen.Default.OpenAIChatRequest));
            var task = client.PostAsync(Server, req);
            task.Wait();

            var rsp = task.Result;
            if (!rsp.IsSuccessStatusCode)
                throw new Exception($"AI service returns error code {rsp.StatusCode}");

            var reader = rsp.Content.ReadAsStringAsync();
            reader.Wait();

            return JsonSerializer.Deserialize(reader.Result, JsonCodeGen.Default.OpenAIChatResponse);
        }
    }
}