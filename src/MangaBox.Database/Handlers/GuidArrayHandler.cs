namespace MangaBox.Database.Handlers;

public class GuidArrayHandler : SqlMapper.TypeHandler<Guid[]>
{
    public override Guid[] Parse(object value)
    {
        if (value is Guid[] guids)
            return guids;

        if (value is string[] array)
            return array.Select(Guid.Parse).ToArray();

        if (value is not string str)
            return [];

        return str
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(Guid.Parse)
            .ToArray();
    }

    public override void SetValue(IDbDataParameter parameter, Guid[] value)
    {
        parameter.Value = value;
    }
}
