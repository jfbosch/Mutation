using System.Reflection;
using System.Runtime.Serialization;

namespace CognitiveSupport.Extensions;

public static class EnumExtensions
{
	public static string ToEnumMemberValue<TEnum>(this TEnum enumValue)
		 where TEnum : struct, Enum
	{
		var type = typeof(TEnum);
		var name = enumValue.ToString();
		var field = type.GetField(name);
		if (field == null)
			throw new ArgumentException($"'{name}' is not a valid member of {type}.");

		var attr = field.GetCustomAttribute<EnumMemberAttribute>();
		return attr?.Value ?? name;
	}
}
