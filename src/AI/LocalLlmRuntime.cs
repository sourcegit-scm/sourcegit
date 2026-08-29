using System;
using System.Threading;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace SourceGit.AI
{
    internal sealed class LocalLlmRuntime : IDisposable
    {
        private LocalLlmRuntime(LLamaWeights weights, ModelParams modelParams)
        {
            _weights = weights;
            _modelParams = modelParams;
        }

        public static LocalLlmRuntime Load(string modelPath, uint contextWindow)
        {
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = contextWindow,
                GpuLayerCount = 0,
            };
            var weights = LLamaWeights.LoadFromFile(parameters);
            return new LocalLlmRuntime(weights, parameters);
        }

        public async Task StreamAsync(string prompt, float temperature, Action<string> onUpdate, CancellationToken cancellationToken)
        {
            var executor = new StatelessExecutor(_weights, _modelParams) { ApplyTemplate = false };
            var inference = new InferenceParams
            {
                MaxTokens = 1024,
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = temperature,
                    TopP = 0.95f,
                },
            };

            await foreach (var chunk in executor.InferAsync(prompt, inference, cancellationToken).WithCancellation(cancellationToken))
                onUpdate?.Invoke(chunk);
        }

        public void Dispose()
        {
            _weights.Dispose();
        }

        private readonly LLamaWeights _weights;
        private readonly ModelParams _modelParams;
    }
}
