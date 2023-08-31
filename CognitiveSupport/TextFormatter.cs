using CognitiveSupport.Extensions;
using System.Text.RegularExpressions;
using static CognitiveSupport.LlmSettings;
using static CognitiveSupport.LlmSettings.TranscriptFormatRule;

namespace CognitiveSupport
{
	public static class TextFormatter
	{
		public static string FormatWithRules(
			this string text,
			List<LlmSettings.TranscriptFormatRule> rules)
		{
			if (text is null) return text;
			if (rules is null) throw new ArgumentNullException(nameof(rules));

			text = text.FixNewLines();

			foreach (var rule in rules)
				text = FormatWithRule(text, rule);

			string[] lines = text.Split(Environment.NewLine, StringSplitOptions.TrimEntries);
			lines = CleanLines(lines);
			text = string.Join(Environment.NewLine, lines);

			return text;
		}

		public static string CleanupPunctuation(
			this string text)
		{
			if (text is null) return text;

			string[] deduplications = new[]
			{
				".",
				",",
				"?",
				"!",
				":",
				";",
			};

			text = text.FixNewLines();
			int counter = 0;
			var lines = text.Split(Environment.NewLine, StringSplitOptions.TrimEntries)
				.ToDictionary(k => counter++, v => v);

			foreach (var dup in deduplications)
			{
				// this will replace the pattern with a single instance of the char in "dup".
				// Eg. If the dup char is ".", then ".." and ". ." wil be replaced with a single ".", but only if it is after a word, and before a word or end of line.
				string pattern = @$"(?<=\w)[.,]?[{dup}][ ,.]?[{dup}]?(?=\w|$)";

				foreach (int key in lines.Keys)
				{
					lines[key]  = Regex.Replace(lines[key], pattern, $"{dup} ");
				}
			}

			text = string.Join(Environment.NewLine, lines.Values);

			return text;
		}

		public static string[] CleanLines(
			this string[] input)
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
			//line = line.RemovePrefix(", ");
			//line = line.RemovePrefix(". ");
			//line = line.RemovePrefix("; ");


			//line = line.Replace("- , ", "- ");
			//line = line.Replace("- . ", "- ");
			//line = line.Replace("- ; ", "- ");

			//line = line.Replace(", : ,", ":");
			//line = line.Replace(". : .", ":");
			//line = line.Replace(". : ,", ":");
			//line = line.Replace(", : .", ":");

			return line;
		}

		public static string FormatWithRule(
			string text,
			TranscriptFormatRule rule)
		{
			if (text is null) return text;
			if (rule is null) throw new ArgumentNullException(nameof(rule));

			RegexOptions regexOptions = RegexOptions.None;
			if (!rule.CaseSensitive)
				regexOptions = RegexOptions.IgnoreCase;

			switch (rule.MatchType)
			{
				case MatchTypeEnum.Plain:
					var comparison = StringComparison.InvariantCultureIgnoreCase;
					if (rule.CaseSensitive)
						comparison = StringComparison.InvariantCulture;

					text = text.Replace(rule.Find, rule.ReplaceWith, comparison);
					break;
				case MatchTypeEnum.RegEx:
					text = Regex.Replace(text, rule.Find, rule.ReplaceWith, regexOptions);
					break;
				case MatchTypeEnum.Smart:
					string pattern = $@"(\b|^)([.,]?)([ ]*{rule.Find}[.,]?[ ]*)(\b|$)";
					string replacement = $"$1$2{rule.ReplaceWith}$4";
					text = Regex.Replace(text, pattern, replacement, regexOptions);

					break;
				default:
					throw new NotImplementedException($"The MatchType {rule.MatchType}: {(int)rule.MatchType} is not implemented.");
			}

			return text;
		}

		public static string RemoveSubstrings(
			this string text,
			params string[] substringsToRemove)
		{
			if (text is null) return text;
			if (substringsToRemove is null || !substringsToRemove.Any())
				throw new ArgumentNullException(nameof(substringsToRemove));

			foreach (var substring in substringsToRemove)
			{
				text = text.Replace(substring, "");
			}

			return text;
		}
	}
}
