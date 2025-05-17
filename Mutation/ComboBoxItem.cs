namespace Mutation;

internal class ComboBoxItem<T>
{
    public string Text { get; set; } = string.Empty;
    public T? Value { get; set; }

    public override string ToString()
    {
        return Text;
    }
}

