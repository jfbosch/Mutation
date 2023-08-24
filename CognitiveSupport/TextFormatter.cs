using CognitiveSupport.Extensions;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using StringExtensionLibrary;
using static CognitiveSupport.LlmSettings;
using static System.Net.Mime.MediaTypeNames;

namespace CognitiveSupport
{
	public class TextFormatter
	{
		public static string Format(
			string text,
			List<LlmSettings.TranscriptFormatRule> rules)
		{
			if (text is null) return text;
			if (rules is null) throw new ArgumentNullException(nameof(rules));

			text = text.FixNewLines();

			foreach (var rule in rules)
				text = Format(text, rule);

			string[] lines = text.Split(Environment.NewLine, StringSplitOptions.TrimEntries);
			lines = CleanLines(lines);
			text = string.Join(Environment.NewLine, lines);

			return text;
		}

		public static string[] CleanLines(
			string[] input)
		{
			List<string> output = new(input.Length);
			foreach (string inLine in input)
			{
				string outLine = CleanLine(inLine);
				output.Add(outLine);
			}
			return output.ToArray();
		}

		private static string CleanLine(
			string line)
		{
			line = line.RemovePrefix(", ");
			line = line.RemovePrefix(". ");
			line = line.RemovePrefix("; ");

			
			line = line.Replace("- , ", "- ");
			line = line.Replace("- . ", "- ");
			line = line.Replace("- ; ", "- ");

			line = line.Replace(", : ,", ":");
			line = line.Replace(". : .", ":");
			line = line.Replace(". : ,", ":");
			line = line.Replace(", : .", ":");

			return line;
		}

		public static string Format(
			string text,
			TranscriptFormatRule rule)
		{
			if (text is null) return text;
			if (rule is null) throw new ArgumentNullException(nameof(rule));

			if (rule.UseRegEx)
			{
				throw new NotImplementedException("RegEx support still to be implemented");
			}
			else
			{
				var comparison = StringComparison.InvariantCultureIgnoreCase;
				if (rule.CaseSensitive)
					comparison = StringComparison.InvariantCulture;

				text = text.Replace(rule.Find, rule.ReplaceWith, comparison);
			}

			return text;
		}
	}
}
