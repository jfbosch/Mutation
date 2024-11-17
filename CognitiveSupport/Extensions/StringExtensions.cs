namespace CognitiveSupport.Extensions;

public static class StringExtensions
{
	/// <summary>
	/// Converts any partial new lines and carriage returns in the given string to the system's new line representation.
	/// If the input string contains only '\n' or '\r', they are replaced with the appropriate new line string for the system.
	/// If a correct new line for the system already exists, it remains unchanged.
	/// </summary>
	/// <param name="text">The input string that may contain partial new lines or carriage returns.</param>
	/// <returns>A new string with all partial new lines and carriage returns replaced by the system's new line representation, or null if the input is null.</returns>
	public static string FixNewLines(
		this string text)
	{
		if (text is null) return text;

		// Replace isolated "\r" with "\n"
		text = text.Replace("\r\n", "\n");
		text = text.Replace("\r", "\n");

		text = text.Replace("\n", Environment.NewLine);

		return text;
	}

}
