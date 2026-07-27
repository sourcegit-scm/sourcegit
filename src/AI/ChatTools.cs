using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace SourceGit.AI
{
    public static class ChatTools
    {
        public static readonly ChatTool GetDetailChangesInFile = ChatTool.CreateFunctionTool(
            "GetDetailChangesInFile",
            "Get the detailed changes in the specified file in the specified repository.",
            BinaryData.FromBytes(Encoding.UTF8.GetBytes("""
            {
                "type": "object",
                "properties": {
                    "file": {
                        "type": "string",
                        "description": "The path to the file."
                    },
                    "originalFile": {
                        "type": "string",
                        "description": "The path to the original file when it has been renamed or copied."
                    }
                 },
                 "required": ["file"]
            }
            """)), false);

        public static async Task<ToolChatMessage> ProcessAsync(ChatToolCall call, string repo, string amendParent, Action<string> output)
        {
            using var doc = JsonDocument.Parse(call.FunctionArguments);

            if (call.FunctionName.Equals(GetDetailChangesInFile.FunctionName))
            {
                var hasFile = doc.RootElement.TryGetProperty("file", out var filePath);
                var hasOriginalFile = doc.RootElement.TryGetProperty("originalFile", out var originalFilePath);
                if (!hasFile)
                    throw new ArgumentException("file", "The file argument is required");

                output?.Invoke($"Read changes in file: {filePath.GetString()}");

                var orgFilePath = hasOriginalFile ? originalFilePath.GetString() : string.Empty;
                var rs = await new Commands.GetFileChangeForAI(repo, filePath.GetString(), orgFilePath, amendParent).ReadAsync();
                var message = rs.IsSuccess ? rs.StdOut : string.Empty;
                return new ToolChatMessage(call.Id, message);
            }

            throw new NotSupportedException($"The tool {call.FunctionName} is not supported");
        }
    }
}
