namespace MangaBox.Database.Handlers;

public class EnumHandler<T> : SqlMapper.TypeHandler<T>
    where T : struct, Enum
{
    public override T Parse(object value)
    {
        return !Enum.TryParse(value?.ToString(), true, out T result)
            ? default
            : result;
    }

    public override void SetValue(IDbDataParameter parameter, T value)
    {
        parameter.Value = (int)(object)value;
    }
}
