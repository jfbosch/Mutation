using System;

namespace CognitiveSupport
{
    public class LlmServiceException : Exception
    {
        public string? ErrorCode { get; }
        public string? ErrorMessage { get; }

        public LlmServiceException()
            : base("LLM service request failed.")
        {
        }

        public LlmServiceException(string message)
            : base(message)
        {
        }

        public LlmServiceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public LlmServiceException(string? errorCode, string? errorMessage)
            : base(FormatMessage(errorCode, errorMessage))
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public LlmServiceException(string? errorCode, string? errorMessage, Exception innerException)
            : base(FormatMessage(errorCode, errorMessage), innerException)
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        private static string FormatMessage(string? errorCode, string? errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(errorCode) && !string.IsNullOrWhiteSpace(errorMessage))
            {
                return $"LLM service request failed with code {errorCode}: {errorMessage}";
            }
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                return $"LLM service request failed: {errorMessage}";
            }
            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                return $"LLM service request failed with code {errorCode}.";
            }
            return "LLM service request failed with an unknown error.";
        }
    }
}
