using System;
using System.IO;

namespace Mutation.Ui;

internal sealed record SpeechSession(string FilePath, DateTime Timestamp)
{
        public string FileName => Path.GetFileName(FilePath);

        public string Extension => Path.GetExtension(FilePath);
}
