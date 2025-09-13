using Mutation.Ui.Services;

namespace Mutation.Tests;

public class SendKeysMapperTests
{
	[Theory]
	[InlineData("Ctrl+V", "^v")]
	[InlineData("CTRL+v", "^v")]
	[InlineData("Ctrl+Delete", "^{DEL}")]
	[InlineData("Ctrl+Alt+Delete", "^%{DEL}")]
	[InlineData("Shift+F10", "+{F10}")]
	[InlineData("Alt+Space", "%{SPACE}")]
	[InlineData("Ctrl++", "^{+}")]
	[InlineData("Ctrl+C, Ctrl+V", "^c^v")]
	[InlineData("^{DEL}", "^{DEL}")]
	[InlineData("AltGr+E", "^%e")]
	[InlineData("PgDn", "{PGDN}")]
	[InlineData("ArrowUp", "{UP}")]
	public void Maps_Common_Inputs(string input, string expected)
	{
		Assert.Equal(expected, SendKeysMapper.Map(input));
	}

	[Fact]
	public void Throws_On_Unsupported_WindowsKey()
	{
		var ex = Assert.Throws<NotSupportedException>(() => SendKeysMapper.Map("Win+E"));
		Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Throws_On_Unknown_Token()
	{
		var ex = Assert.Throws<FormatException>(() => SendKeysMapper.Map("Ctrl+FooKey"));
		Assert.Contains("Unknown key name", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Throws_On_Missing_Primary()
	{
		var ex = Assert.Throws<FormatException>(() => SendKeysMapper.Map("Ctrl+Shift"));
		Assert.Contains("No primary key", ex.Message, StringComparison.OrdinalIgnoreCase);
	}
}
