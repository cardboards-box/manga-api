namespace MangaBox.Database.Generation.Services;

using Models;

internal interface IFileGenerationService
{
    Task TableCreate(TableEntity table, Entities entities, StreamWriter writer, int asOf);

    Task TypeCreate(TypeEntity type, Entities entities, StreamWriter writer, int asOf);

    Task ArrayJoinCreate(TypeEntity type, Entities entities, StreamWriter writer);

    Task OrmClass(TableEntity table, StreamWriter writer, int asOf, string nameSpace, Entities entities, string appName);

    Task DbServiceClass(Entities entities, StreamWriter writer, string nameSpace, string prefix);

    Task ResolverExtensions(Entities entities, StreamWriter writer);

    Task DropTables(Entities entities, StreamWriter writer);
}

internal class FileGenerationService(
    ILogger<FileGenerationService> _logger,
    IDatabaseMetadataService _metadata) : IFileGenerationService
{
    public static string? DetermineSqlBasicType(Type type)
    {
        if (type == typeof(string)) return "TEXT";
        if (type == typeof(int) || type.IsEnum) return "INTEGER";
        if (type == typeof(long)) return "BIGINT";
        if (type == typeof(Guid)) return "UUID";
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "TIMESTAMP";
        if (type == typeof(bool)) return "BOOLEAN";
        if (type == typeof(decimal)) return "DECIMAL";
        if (type == typeof(double) || type == typeof(float)) return "NUMERIC";
        if (type == typeof(short)) return "SMALLINT";
        if (type == typeof(byte)) return "TINYINT";

        return null;
    }

    public static bool IsArrayType(Type type, out Type baseType)
    {
        if (type.IsArray)
        {
            baseType = type.GetElementType()!;
            return true;
        }

        if (typeof(IEnumerable<>).IsAssignableFrom(type))
        {
            baseType = type.GenericTypeArguments[0];
            return true;
        }

        baseType = type;
        return false;
    }

    public static string? DetermineColumnType(TableColumn column, Entities entities, out TypeEntity? udt, out bool isArray, bool skipTyping = false)
    {
        const string PK = " DEFAULT uuid_generate_v4()";
        udt = null;
        var type = column.Type;
        isArray = false;
        if (type == typeof(Guid) && column.IsPrimaryKey) return $"UUID{(skipTyping ? "" : PK)}";

        var array = isArray = IsArrayType(type, out type);
        var nullable = Nullable.GetUnderlyingType(type) is not null;
        if (nullable) type = Nullable.GetUnderlyingType(type)!;

        var str = DetermineSqlBasicType(type);
        if (string.IsNullOrEmpty(str))
        {
            udt = entities.Types.FirstOrDefault(entities => entities.Type == type);
            if (udt is null) return null;

            str = udt.Name;
        }

        if (skipTyping)
        {
            var typeStr = str.ForceNull();
            return typeStr is not null && array ? $"{typeStr}[]" : typeStr;
        }

        if (type == typeof(string) && !column.Required) nullable = true;

        if (column.Required)
            nullable = false;


        if (array) return $"{str}[] DEFAULT '{{}}'";
        return $"{str}{(nullable ? " NULL" : " NOT NULL")}{(column.Default is null ? "" : " DEFAULT " + column.Default)}";
    }

    public async Task TableCreate(TableEntity table, Entities entities, StreamWriter writer, int asOf)
    {
        async Task WriteTableColumn(TableColumn column)
        {
            await writer.WriteAsync(column.Name + " ");
            var type = DetermineColumnType(column, entities, out var udt, out _);
            if (type.ForceNull() is null) throw new NullReferenceException($"Column type came back null: {table.GetFullName()} >> {column.Name}");

            if (udt is not null)
                table.Requires.Add(udt.Type);

            await writer.WriteAsync(type);

            if (column.IsUnique && table.UniqueCols.Count() == 1)
            {
                await writer.WriteAsync(" UNIQUE");
                if (!column.Required)
                    await writer.WriteAsync(" NULLS NOT DISTINCT");
            }
            if (column.IsPrimaryKey)
                await writer.WriteAsync(" PRIMARY KEY");

            if (!column.IsFk) return;

            var fk = column.ForeignKey!;
            var fkTable = entities.Tables.FirstOrDefault(t => t.Type == fk.Type)
                ?? throw new NullReferenceException($"Foreign key table not found: {table.GetFullName()} >> {column.Name} >> {fk.Type.Name}");

            var fkProperty = fkTable.Columns.FirstOrDefault(t => t.Property.Name == fk.Property)
                ?? throw new NullReferenceException($"Foreign key property not found: {table.GetFullName()} >> {column.Name} >> {fk.Type.Name} >> {fk.Property}");
            await writer.WriteAsync($" REFERENCES {fkTable.GetFullName()}({fkProperty.Name})");
        }

        async Task WriteFtsColumn(SearchableAttribute attribute)
        {
            var tableName = table.GetFullName();
            await writer.WriteLineAsync();
            await writer.WriteLineAsync($"ALTER TABLE {tableName}");
            await writer.WriteLineAsync("ADD COLUMN IF NOT EXISTS");
            await writer.WriteLineAsync($"\t{attribute.ColumnName} tsvector GENERATED ALWAYS AS (");
            await writer.WriteLineAsync($"\t\tto_tsvector('{attribute.Language}',");

            for(var i = 0; i < attribute.Fields.Length; i++)
            {
                var field = attribute.Fields[i];
                var isLast = i == attribute.Fields.Length - 1;
                var column = table.Columns.FirstOrDefault(t => t.Property.Name == field)
                    ?? throw new NullReferenceException($"Searchable field not found: {tableName} >> {attribute.ColumnName} >> {field}");
                var type = DetermineColumnType(column, entities, out var udt, out var isArray, true)?.ForceNull()
                    ?? throw new NullReferenceException($"Could not determine data type: {tableName} >> {attribute.ColumnName} >> {field}");
                string? colRef = null;
                if (type == "TEXT")
                    colRef = column.Name;
                else if (udt is not null && isArray)
                {
                    if (udt.ArrayJoin is null)
                        throw new NullReferenceException($"Array join not defined for array type: {tableName} >> {attribute.ColumnName} >> {field} >> {udt.Name}");
                    colRef = $"{udt.ArrayJoin.FullName}({column.Name}, ' ')";
                }
                else if (isArray && type == "TEXT[]")
                    colRef = $"text_array_join({column.Name}, ' ')";
                else if (isArray)
                    throw new Exception($"Could not determine property column reference to use for search field: {tableName} >> {attribute.ColumnName} >> {field} >> {type}");
                else
                    colRef = $"CAST({column.Name} AS TEXT)";

                await writer.WriteAsync($"\t\t\t{colRef}");
                if (!isLast)
                    await writer.WriteAsync(" || ' ' ||");
                await writer.WriteLineAsync();
            }

            await writer.WriteLineAsync("\t\t)");
            await writer.WriteLineAsync("\t) STORED;");
        }

        await writer.WriteLineAsync($"CREATE TABLE IF NOT EXISTS {table.GetFullName()} (");

        var firstCol = true;
        foreach (var column in table.Columns)
        {
            if (column.Ignore || column.AsOf > asOf) continue;

            if (!firstCol)
                await writer.WriteLineAsync(',');

            await writer.WriteAsync('\t');
            await WriteTableColumn(column);
            firstCol = false;
        }

        var uniqueCols = table.UniqueCols.ToArray();
        if (uniqueCols.Length > 1)
        {
            await writer.WriteLineAsync(',');
            await writer.WriteAsync($"\tCONSTRAINT {table.Name}_unique UNIQUE");
            var anyNullCols = uniqueCols.Any(t => !t.Required);
            if (anyNullCols)
                await writer.WriteAsync(" NULLS NOT DISTINCT");

            await writer.WriteAsync(" (");
			firstCol = true;
            foreach (var col in uniqueCols)
            {
                if (!firstCol)
                    await writer.WriteAsync(", ");

                await writer.WriteAsync(col.Name);
                firstCol = false;
            }
            await writer.WriteAsync(")");
        }

        await writer.WriteLineAsync();
        await writer.WriteLineAsync(");");

        var recentlyAdded = table.Columns.Where(t => !t.Ignore && t.AsOf <= asOf && t.AsOf != 0);
        foreach (var column in recentlyAdded)
        {
            await writer.WriteLineAsync();
            await writer.WriteAsync($"ALTER TABLE {table.GetFullName()} ADD COLUMN IF NOT EXISTS ");
            await WriteTableColumn(column);
            await writer.WriteAsync(';');
        }

        foreach (var attribute in table.Searchables)
            if (attribute.Fields.Length > 0)
                await WriteFtsColumn(attribute);

        await writer.FlushAsync();

        _logger.LogInformation("Finished writing table: {table}", table.Name);
    }

    public async Task TypeCreate(TypeEntity type, Entities entities, StreamWriter writer, int asOf)
    {
        async Task WriteTypeColumn(TableColumn column)
        {
            await writer.WriteAsync(column.Name + " ");
            var colType = DetermineColumnType(column, entities, out var udt, out _, true);
            if (colType.ForceNull() is null) throw new NullReferenceException($"Column type came back null: {type.Name} >> {column.Name}");

            if (udt is not null)
                type.Requires.Add(udt.Type);

            await writer.WriteAsync(colType);
        }

        await writer.WriteLineAsync("DO $$");
        await writer.WriteLineAsync("BEGIN");
        await writer.WriteLineAsync("\tIF NOT EXISTS (");
        await writer.WriteLineAsync($"\t\tSELECT 1");
        await writer.WriteLineAsync($"\t\tFROM pg_type");
        await writer.WriteLineAsync($"\t\tWHERE typname = '{type.Name}'");
        await writer.WriteLineAsync("\t) THEN");
        await writer.WriteLineAsync($"\t\tCREATE TYPE {type.Name} AS (");

        var firstCol = true;
        foreach (var column in type.Columns)
        {
            if (column.Ignore || column.AsOf > asOf) continue;

            if (!firstCol)
                await writer.WriteLineAsync(',');

            await writer.WriteAsync("\t\t\t");
            await WriteTypeColumn(column);
            firstCol = false;
        }

        await writer.WriteLineAsync();
        await writer.WriteLineAsync("\t\t);");
        await writer.WriteLineAsync("\tEND IF;");
        await writer.WriteLineAsync("END$$;");
        await writer.FlushAsync();
    }

    public static string ClassNameFromType(TableEntity table)
    {
        return table.Type.Name + "DbService";
    }

    public async Task OrmClass(TableEntity table, StreamWriter writer, int asOf, string nameSpace, Entities entities, string appName)
    {
        const string METHODS_UPSERT = @"

    /// <summary>
    /// Inserts a record in the {tableName} table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name=""item"">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert({type} item);";
        const string CACHE_TYPE = "Cache";
        const string BASE_SCRIPT = @"namespace {nameSpace};

using Models;
using Models.Composites;
using Models.Types;

/// <summary>
/// The service for interacting with the {tableName} table
/// </summary>
public interface I{className}
{
    /// <summary>
    /// Fetches a record by its ID from the {tableName} table
    /// </summary>
    /// <param name=""id"">The ID of the record</param>
    /// <returns>The record</returns>
    Task<{type}?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the {tableName} table
    /// </summary>
    /// <param name=""item"">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert({type} item);

    /// <summary>
    /// Updates a record in the {tableName} table
    /// </summary>
    /// <param name=""item"">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update({type} item);{upsert}

    /// <summary>
    /// Gets all of the records from the {tableName} table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<{type}[]> Get();{relationshipInterface}
}

internal class {className}(
    IOrmService orm) : {cache}Orm<{type}>(orm), I{className}
{
{relationshipMethod}
}";
        const string RELATIONSHIP_NAME = "FetchWithRelationships";

        void GenerateOtmRelationship(OneToManyRelationship relationship, List<string> queries, List<string> readers)
        {
            if (relationship.Many != table.Type)
                return;

            var child = table;
            var parent = entities.Tables.FirstOrDefault(t => t.Type == relationship.One);
            if (parent is null)
            {
                _logger.LogInformation("Could not find related entity: {table} >> {one} -> {many}",
                    table.GetFullName(), relationship.One.Name, relationship.Many.Name);
                return;
            }

            var parentColumn = parent.Columns.FirstOrDefault(t => t.Property.Name == relationship.Attribute.Property);
            if (parentColumn is null)
            {
                _logger.LogInformation("Could not find related parent column: {table} >> {one} -> {many} >> {related}",
                    table.GetFullName(), relationship.One.Name, relationship.Many.Name, parent.Name);
                return;
            }

            var children = child.Columns.Where(t => t.ForeignKey == relationship.Attribute);
            if (!children.Any())
            {
                _logger.LogInformation("Could not find related child column: {table} >> {one} -> {many} >> {related}",
                    table.GetFullName(), relationship.One.Name, relationship.Many.Name, child.Name);
                return;
            }

            var relationships = string.Join(" OR ", children.Select(t => $"p.{parentColumn.Name} = c.{t.Name}"));

            var tableName = parent.GetFullName();
            queries.Add($@"SELECT p.* 
FROM {parent.GetFullName()} p
JOIN {table.GetFullName()} c ON {relationships}
WHERE 
    c.id = :id AND
    c.deleted_at IS NULL AND
    p.deleted_at IS NULL;
");
            readers.Add($"{appName}Relationship.Apply(related, await rdr.ReadAsync<{parent.Type.Name}>());");
        }

        void GenerateBrdRelationship(BridgeRelationship relationship, List<string> queries, List<string> readers)
        {
            var shouldInclude = (relationship.Child.Table.Type == table.Type && relationship.Attribute.IncludeInChildOrm) ||
                (relationship.Parent.Table.Type == table.Type && relationship.Attribute.IncludeInParentOrm);
            if (!shouldInclude) return;

            var isParent = relationship.Parent.Table.Type == table.Type;
            var entity = isParent ? relationship.Parent : relationship.Child;
            var notEntity = isParent ? relationship.Child : relationship.Parent;

            var parentTable = relationship.Parent.Table.GetFullName();
            var parentColumn = relationship.Parent.Column.Name;
            var parentBridge = relationship.Parent.BridgeColumn.Name;

            var childTable = relationship.Child.Table.GetFullName();
            var childColumn = relationship.Child.Column.Name;
            var childBridge = relationship.Child.BridgeColumn.Name;

            var bridgeTable = relationship.Bridge.GetFullName();

            var query = isParent 
                ? $"p.{entity.Column.Name} = :id" 
                : $"c.{entity.Column.Name} = :id";

            queries.Add($@"SELECT DISTINCT {(isParent ? 'c' : 'p')}.*
FROM {parentTable} p
JOIN {bridgeTable} b ON b.{parentBridge} = p.{parentColumn}
JOIN {childTable} c ON c.{childColumn} = b.{childBridge}
WHERE 
    {query} AND
    p.deleted_at IS NULL AND
    b.deleted_at IS NULL AND
    c.deleted_at IS NULL;
");
            readers.Add($"{appName}Relationship.Apply(related, await rdr.ReadAsync<{notEntity.Table.Type.Name}>());");
        }

        void GenerateAudRelationship(Entities all, List<string> queries, List<string> readers)
        {
            //Skip if the current table isn't auditable
            if (table.Audit is null) return;

            //Find the audit table and it's column names
            var auditTblName = table.GetFullName();
            var auditColTpName = table.Columns.FirstOrDefault(t => t.Property.Name == table.Audit.TypeColumnName)?.Name;
            var auditColIdName = table.Columns.FirstOrDefault(t => t.Property.Name == table.Audit.IdColumnName)?.Name;
            var auditPk = table.Columns.FirstOrDefault(t => t.IsPrimaryKey)?.Name;
            //If we can't find the audit columns, skip this
            if (string.IsNullOrEmpty(auditColTpName) || 
                string.IsNullOrEmpty(auditColIdName) ||
                string.IsNullOrEmpty(auditPk))
            {
                _logger.LogInformation("Could not find audit columns for table: {table} >> {type} :: {id} >> {pk}", 
                    auditTblName, auditColTpName, auditColIdName, auditPk);
                return;
            }

            //Iterate through all of the tables to build the maps
            foreach (var entity in all.Tables)
            {
                //Skip any entities that are audit entities
                if (entity.Audit is not null) continue;
                //Get the type name
                var typeName = entity.InterfaceOption?.Names?.FirstOrDefault() ?? entity.Type.Name;
                //Get the primary key column for the entity
                var pk = entity.Columns.FirstOrDefault(t => t.IsPrimaryKey);
                if (pk is null) continue;
                //Add the queries and readers
                queries.Add($@"SELECT e.*
FROM {auditTblName} a
JOIN {entity.GetFullName()} e ON e.{pk.Name} = a.{auditColIdName}
WHERE 
    a.{auditColTpName} = '{typeName}' AND 
    a.{auditPk} = :id AND
    a.deleted_at IS NULL AND
    e.deleted_at IS NULL;
");
                readers.Add($"{appName}Relationship.Apply(related, await rdr.ReadAsync<{entity.Type.Name}>());");
            }
        }

        string GenerateRelationshipMethod()
        {
            string METHOD_DELCARATION = @"
    public async Task<{5}Type<{0}>?> {1}(Guid id)
    {{
        const string QUERY = @""SELECT * FROM {4} WHERE id = :id AND deleted_at IS NULL;
{2}"";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new {{ id }});

        var item = await rdr.ReadSingleOrDefaultAsync<{0}>();
        if (item is null) return null;

        var related = new List<{5}Relationship>();
        {3}

        return new {5}Type<{0}>(item, [..related]);
    }}";
            var type = table.Type.Name;
            var tableName = table.GetFullName();

            var queries = new List<string>();
            var readers = new List<string>();

            foreach(var relationship in entities.Relationships)
            {
                if (relationship is OneToManyRelationship otm)
                    GenerateOtmRelationship(otm, queries, readers);

                if (relationship is BridgeRelationship bridge)
                    GenerateBrdRelationship(bridge, queries, readers);
            }

            GenerateAudRelationship(entities, queries, readers);

            if (queries.Count == 0) return string.Empty;

            return string.Format(
                METHOD_DELCARATION, 
                type, 
                RELATIONSHIP_NAME, 
                string.Join("\r\n", queries),
                string.Join("\r\n\t\t", readers),
                tableName,
                appName);
        }

        (string method, string inter) GenerateRelationship()
        {
            var method = GenerateRelationshipMethod();
            if (string.IsNullOrEmpty(method))
                return (string.Empty, string.Empty);

            var type = table.Type.Name;
            return (method, $@"

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name=""id"">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<{appName}Type<{type}>?> {RELATIONSHIP_NAME}(Guid id);");
        }

        var upsert = table.UniqueCols.Any() ? METHODS_UPSERT : string.Empty;
        var cache = table.Cache ? CACHE_TYPE : string.Empty;
        var type = table.Type.Name;
        var className = ClassNameFromType(table);
        var tableName = table.GetFullName();
        var (relationshipMethod, relationshipInterface) = GenerateRelationship();

        var replacers = new Dictionary<string, string>
        {
            [nameof(upsert)] = upsert,
            [nameof(cache)] = cache,
            [nameof(type)] = type,
            [nameof(nameSpace)] = nameSpace,
            [nameof(className)] = className,
            [nameof(tableName)] = tableName,
            [nameof(relationshipInterface)] = relationshipInterface,
            [nameof(relationshipMethod)] = relationshipMethod
        };

        var script = BASE_SCRIPT;
        foreach (var (key, value) in replacers)
            script = script.Replace($"{{{key}}}", value);

        await writer.WriteAsync(script);
    }

    public async Task DbServiceClass(Entities entities, StreamWriter writer, string nameSpace, string prefix)
    {
        string TrimPrefix(string str)
        {
            if (!str.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
                return str;

            return str[prefix.Length..];
		}

        var tables = entities.Tables.OrderBy(t => t.Type.Name).ToArray();

        await writer.WriteLineAsync($"namespace {nameSpace.Replace(".Services", "")};");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("using Services;");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync(@"/// <summary>
/// The service for interacting with the database
/// </summary>
public interface IDbService");
        await writer.WriteLineAsync("{");

        var first = true;
        foreach (var entity in tables)
        {
            if (!first)
                await writer.WriteLineAsync();

            var table = entity.GetFullName();
            await writer.WriteLineAsync($@"    /// <summary>
    /// The service for interacting with the {table} table
    /// </summary>");
            await writer.WriteLineAsync($"\tI{ClassNameFromType(entity)} {TrimPrefix(entity.Type.Name)} {{ get; }}");
            first = false;
        }

        await writer.WriteLineAsync("}");
        await writer.WriteLineAsync();
        await writer.WriteLineAsync(@"internal class DbService(IServiceProvider _provider) : IDbService");
        await writer.WriteLineAsync("{");

        await writer.WriteLineAsync("\t#region Lazy Loaded Service Caches");
        foreach (var entity in tables)
        {
            await writer.WriteLineAsync($"\tprivate I{ClassNameFromType(entity)}? _{TrimPrefix(entity.Type.Name).ToPascalCase()};");
        }
        await writer.WriteLineAsync("\t#endregion");

        await writer.WriteLineAsync();

        await writer.WriteLineAsync("\t#region Service Implementations");
        foreach (var entity in tables)
        {
            await writer.WriteLineAsync($"\tpublic I{ClassNameFromType(entity)} {TrimPrefix(entity.Type.Name)} => _{TrimPrefix(entity.Type.Name).ToPascalCase()} ??= _provider.GetRequiredService<I{ClassNameFromType(entity)}>();");
        }
        await writer.WriteLineAsync("\t#endregion");

        await writer.WriteLineAsync("}");
        await writer.WriteLineAsync();
        await writer.FlushAsync();
    }

    public async Task ResolverExtensions(Entities entities, StreamWriter writer)
    {
        foreach (var entity in entities.Types)
            await writer.WriteLineAsync($"\t\t\t.AddType<{entity.Type.Name}>()");

        await writer.WriteLineAsync();

        foreach (var table in entities.Tables.OrderBy(t => t.Type.Name.Length).ThenBy(t => t.Type.Name))
        {
            var className = ClassNameFromType(table);
            await writer.WriteLineAsync($"\t\t\t.Add<I{className}, {className}, {table.Type.Name}>()");
        }

        await writer.WriteLineAsync();

        var enums = entities.Enums.OrderBy(t => t.Name.Length).ToArray();
        if (enums.Length == 0) return;

        await writer.WriteLineAsync("\t\t\t.Mapping(c => c");

        foreach (var enumType in enums)
        {
            await writer.WriteLineAsync($"\t\t\t\t.Enum<{enumType.Name}>()");
        }

        await writer.WriteLineAsync("\t\t\t\t.PolyfillGuidArrays())");
    }

    public async Task DropTables(Entities entities, StreamWriter writer)
    {
        var thingsToDrop = _metadata.OrderDependencies(entities).Reverse();

        foreach (var entity in thingsToDrop)
        {
            if (entity is TypeEntity type)
            {
                if (type.ArrayJoin is not null)
                    await writer.WriteLineAsync($"DROP FUNCTION IF EXISTS {type.ArrayJoin.FullName};");

                await writer.WriteLineAsync($"DROP TYPE IF EXISTS {type.Name};");
                continue;
            }

            if (entity is not TableEntity table) continue;

            await writer.WriteLineAsync($"DROP TABLE IF EXISTS {table.GetFullName()};");
        }
        
        await writer.FlushAsync();
    }

    public async Task ArrayJoinCreate(TypeEntity type, Entities entities, StreamWriter writer)
    {
        ArgumentNullException.ThrowIfNull(type.ArrayJoin, nameof(type));

        var name = type.ArrayJoin.FullName;
        var typeName = type.Name;

        var column = type.Columns.FirstOrDefault(t => t.Property.Name == type.ArrayJoin.Property)
            ?? throw new NullReferenceException($"Could not find array join property: {type.Name} >> {type.ArrayJoin.Property}");

        await writer.WriteLineAsync("CREATE OR REPLACE");
        await writer.WriteLineAsync($"FUNCTION {name} (");
        await writer.WriteLineAsync($"\t{typeName}[],");
        await writer.WriteLineAsync("\ttext");
        await writer.WriteLineAsync(") RETURNS text LANGUAGE sql");
        await writer.WriteLineAsync("IMMUTABLE AS $$");
        await writer.WriteLineAsync("\tSELECT array_to_string(ARRAY(");
        await writer.WriteLineAsync($"\t\tSELECT elem.{column.Name}");
        await writer.WriteLineAsync("\t\tFROM unnest($1) AS elem");
        await writer.WriteLineAsync($"\t\tWHERE elem.{column.Name} IS NOT NULL");
        await writer.WriteLineAsync("\t), $2)");
        await writer.WriteLineAsync("$$;");
    }
}
