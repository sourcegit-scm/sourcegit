using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.AI
{
    public class ExtraHeadersPolicy : PipelinePolicy
    {
        public ExtraHeadersPolicy(string headers)
        {
            _headers = headers;
        }

        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Apply(message);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            Apply(message);
            await ProcessNextAsync(message, pipeline, currentIndex);
        }

        private void Apply(PipelineMessage message)
        {
            var lines = _headers.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var idx = line.IndexOf(':');
                if (idx < 0)
                    continue;

                var key = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                    continue;

                message.Request.Headers.Add(key, value);
            }
        }

        private readonly string _headers;
    }
}
