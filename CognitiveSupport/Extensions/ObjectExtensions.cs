﻿namespace CognitiveSupport.Extensions;

public static class ObjectExtensions
{
        public static void Beep(
                this object caller,
                int attempt)
        {
#pragma warning disable CA1416 // Validate platform compatibility
                for (int i = 0; i < attempt; i++)
                        BeepPlayer.Play ( BeepType.End );
#pragma warning restore CA1416 // Validate platform compatibility
        }

}