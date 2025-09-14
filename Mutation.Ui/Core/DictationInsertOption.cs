using System.ComponentModel;

namespace Mutation.Ui;

public enum DictationInsertOption
{
	[Description("Don't insert into 3rd party application")]
	DoNotInsert,

	[Description("Send keys to 3rd party application")]
	SendKeys,

	[Description("Paste into 3rd party application")]
	Paste
}

