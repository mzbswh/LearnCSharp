[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
sealed class AutoNotifyAttribute : Attribute
{
    public string PropertyName { get; set; } = string.Empty;

    public AutoNotifyAttribute() { }
}