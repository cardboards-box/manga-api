namespace MangaBox.Database.Generation.Models;

/// <summary>
/// The column of a table
/// </summary>
/// <param name="Property">The property that represents the column</param>
/// <param name="Required">Whether or not the value of the column is required</param>
/// <param name="Column">The attribute data of the column</param>
/// <param name="ForeignKey">The optional FK attribute for the column</param>
/// <param name="AddedIn">The optional attribute listing the version the column was added in</param>
internal record class TableColumn(
    PropertyInfo Property,
    bool Required,
    ColumnAttribute? Column,
    FkAttribute? ForeignKey,
    AddedInAttribute? AddedIn)
{
    public string Name => Column?.Name?.ForceNull() ?? Property.Name.ToLower();

    public Type Type => Property.PropertyType;

    public bool IsUnique => Column?.Unique ?? false;

    public bool IsFk => ForeignKey is not null;

    public bool IsPrimaryKey => Column?.PrimaryKey ?? false;

    public bool IsEnum => Type.IsEnum;

    public bool Ignore => Column?.Ignore ?? false;

    public string? Default => Column?.OverrideValue;

    public int AsOf => AddedIn?.Version ?? 0;
}