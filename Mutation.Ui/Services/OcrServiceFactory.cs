using CognitiveSupport;
using System;

namespace Mutation.Ui.Services;

public sealed class OcrServiceFactory
{
        public IOcrService Create(AzureComputerVisionSettings? settings)
        {
                if (settings is null)
                        throw new InvalidOperationException("Azure Computer Vision settings must be provided.");

                if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Endpoint))
                        throw new InvalidOperationException("Provide both an API key and endpoint for OCR.");

                int timeout = settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 10;
                return new OcrService(settings.ApiKey, settings.Endpoint, timeout);
        }
}
