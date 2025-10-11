using CognitiveSupport;
using Deepgram;
using OpenAI;
using OpenAI.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace Mutation.Ui.Services;

public sealed class SpeechToTextServiceFactory
{
        private const string OpenAiClientName = "openai-http-client";
        private readonly IHttpClientFactory _httpClientFactory;

        public SpeechToTextServiceFactory(IHttpClientFactory httpClientFactory)
        {
                _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public IReadOnlyList<ISpeechToTextService> CreateAll(IEnumerable<SpeechToTextServiceSettings> settings)
        {
                if (settings is null)
                        return Array.Empty<ISpeechToTextService>();

                return settings.Select(Create).ToList();
        }

        public ISpeechToTextService Create(SpeechToTextServiceSettings settings)
        {
                if (settings is null)
                        throw new ArgumentNullException(nameof(settings));

                return settings.Provider switch
                {
                        SpeechToTextProviders.OpenAi => CreateOpenAiService(settings),
                        SpeechToTextProviders.Deepgram => CreateDeepgramService(settings),
                        _ => throw new NotSupportedException($"The SpeechToText provider '{settings.Provider}' is not supported.")
                };
        }

        private ISpeechToTextService CreateOpenAiService(SpeechToTextServiceSettings settings)
        {
                var options = new OpenAiOptions
                {
                        ApiKey = settings.ApiKey ?? string.Empty,
                        BaseDomain = settings.BaseDomain?.Trim() ?? string.Empty,
                };

                HttpClient httpClient = _httpClientFactory.CreateClient(OpenAiClientName);
                var openAiService = new OpenAIService(options, httpClient);

                return new OpenAiSpeechToTextService(
                        settings.Name ?? string.Empty,
                        openAiService,
                        settings.ModelId ?? string.Empty,
                        settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 10);
        }

        private ISpeechToTextService CreateDeepgramService(SpeechToTextServiceSettings settings)
        {
                var client = ClientFactory.CreateListenRESTClient(settings.ApiKey ?? string.Empty);
                return new DeepgramSpeechToTextService(
                        settings.Name ?? string.Empty,
                        client,
                        settings.ModelId ?? string.Empty,
                        settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 10);
        }
}
