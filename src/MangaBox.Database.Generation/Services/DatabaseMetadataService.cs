namespace MangaBox.Database.Generation.Services;

using Models;

internal interface IDatabaseMetadataService
{
    Entities GetEntities(string tableDir, string typeDir, string funcDir, string? prefix);

    string[] GenerateManifestList(Entities entities, char dirJoiner, string[] before, string[] after);

    IEnumerable<IEntity> OrderDependencies(Entities entities);
}

internal class DatabaseMetadataService(
    IEnumerable<IDbModel> _models,
    ILogger<DatabaseMetadataService> _logger) : IDatabaseMetadataService
{
    public static IEnumerable<TableColumn> GetColumns(Type table, HashSet<Type> requirements)
    {
        var properties = table.GetProperties();
        foreach (var property in properties)
        {
            var column = property.GetCustomAttribute<ColumnAttribute>();
            var fk = property.GetCustomAttribute<FkAttribute>();
            if (fk is not null)
                requirements.Add(fk.Type);
            var addedIn = property.GetCustomAttribute<AddedInAttribute>();
            var required = property.GetCustomAttribute<RequiredAttribute>() is not null;
            yield return new TableColumn(property, required, column, fk, addedIn);
        }
    }

    public IEnumerable<TableEntity> GetTables(string dir, string? prefix)
    {
        foreach (var model in _models)
        {
            if (model is not IDbTable dbType) continue;

            var type = model.GetType();
            var table = type.GetCustomAttribute<TableAttribute>();
            var audit = type.GetCustomAttribute<AuditAttribute>();
            var requirements = new HashSet<Type>();
            var columns = GetColumns(type, requirements);
            var fileName = $"{prefix?.ForceNull() ?? string.Empty}{table?.Name ?? type.Name}.sql";
            var path = Path.Combine(dir, fileName);
            var option = type.GetCustomAttribute<InterfaceOptionAttribute>();
            var bridge = type.GetCustomAttributes<BridgeTableAttribute>().ToArray();
            var searchables = type.GetCustomAttributes<SearchableAttribute>().ToArray();
            yield return new TableEntity(type, dbType, table, audit, searchables, bridge, option, path, requirements, [.. columns.OrderBy(t => !t.IsPrimaryKey)]);
        }
    }

    public IEnumerable<TypeEntity> GetTypes(string dir, string funcDir, string? prefix)
    {
        foreach (var model in _models)
        {
            if (model is not IDbType dbType) continue;

            var type = model.GetType();
            var table = type.GetCustomAttribute<TypeAttribute>();
            var arrayJoin = type.GetCustomAttribute<CreateArrayJoinAttribute>();
            var requirements = new HashSet<Type>();
            var columns = GetColumns(type, requirements);
            var fileName = $"{prefix?.ForceNull() ?? string.Empty}{table?.Name ?? type.Name}.sql";
            var path = Path.Combine(dir, fileName);
            var fpath = arrayJoin is null ? null : Path.Combine(funcDir, arrayJoin.FullName + ".sql");
            yield return new TypeEntity(type, dbType, table, arrayJoin, path, fpath, requirements, [.. columns]);
        }
    }

    public Entities GetEntities(string tableDir, string typeDir, string funcDir, string? prefix)
    {
        var tables = GetTables(tableDir, prefix).ToArray();
        return new(
            tables,
            [..GetTypes(typeDir, funcDir, prefix)],
            [..Relationships(tables)]);
    }

    public IEnumerable<IEntity> OrderDependencies(Entities entities)
    {
        var all = entities.All.ToList();
        var handled = new HashSet<Type>();

        //Keep iterating until there are no deps left
        while(all.Count > 0)
        {
            //Keep track if we made a change in this iteration
            //If this is ever false after the pass, we should
            //throw an error because it probably means there
            //is a circular or missing dependency 
            bool didChange = false;
            foreach(var item in all.ToArray())
            {
                //Item was probably removed in a previous pass
                if (!all.Contains(item)) continue;

                //We already handled this one, so skip it
                if (handled.Contains(item.Type))
                {
                    all.Remove(item);
                    didChange = true;
                    continue;
                }

                //No requirements, so we can handle it
                if (item.Requires.Count == 0)
                {
                    handled.Add(item.Type);
                    all.Remove(item);
                    didChange = true;
                    yield return item;
                    continue;
                }

                //Check all of the deps, if we don't have all of them yet,
                //we can continue to the next pass
                var notMatched = item.Requires.Any(t => !handled.Contains(t) && item.Type != t);
                if (notMatched)
                    continue;

                //We have all of the deps, so we can handle this one
                handled.Add(item.Type);
                all.Remove(item);
                didChange = true;
                yield return item;
            }

            //If we made a change and there are still items left,
            //we can make another pass
            if (didChange) continue;

            _logger.LogError("Missing or circular dependency detected while resolving dependency order: {deps}", all.Count);
            foreach (var item in all)
            {
                _logger.LogError("\tFailed to resolve dependencies for: {type}", item.Type.Name);
                foreach (var requirement in item.Requires)
                {
                    if (handled.Contains(requirement)) continue;
                    _logger.LogError("\t\tRequires: {type} >> NOT FOUND", requirement.Name);
                }
            }

            throw new InvalidOperationException("Failed to resolve all dependencies");
        }
    }

    public IEnumerable<IEntityRelationship> Relationships(IEnumerable<TableEntity> entities)
    {
        BridgeEntity? FetchBridge(TableEntity entity, BridgeTableAttribute bridge, Type type)
        {
            var table = entities.FirstOrDefault(t => t.Type == type);
            if (table is null)
            {
                _logger.LogError("Could not find entity for bridge relationship: {entity} >> {type}",
                    entity.Type.Name, type.Name);
                return null;
            }

            var bridgeColumn = entity.Columns.FirstOrDefault(t => t.ForeignKey?.Type == type);
            if (bridgeColumn is null)
            {
                _logger.LogError("Could not find column for bridge relationship: {entity} >> {type}",
                    entity.Type.Name, type.Name);
                return null;
            }

            var targetColumn = table.Columns.FirstOrDefault(t => t.Property.Name == bridgeColumn.ForeignKey?.Property);
            if (targetColumn is null)
            {
                _logger.LogError("Could not find target column for bridge relationship: {entity} >> {type} >> {column}",
                    table.Type.Name, type.Name, bridgeColumn.ForeignKey?.Property);
                return null;
            }

            return new(table, targetColumn, bridgeColumn);
        }

        IEnumerable<BridgeRelationship> GetBridges(TableEntity entity, HashSet<FkAttribute> bridges)
        {
            foreach(var bridge in entity.Bridges)
            {
                var parent = FetchBridge(entity, bridge, bridge.Parent);
                if (parent is null) continue;

                var child = FetchBridge(entity, bridge, bridge.Child);
                if (child is null) continue;

                bridges.Add(parent.Column.ForeignKey!);
                bridges.Add(child.Column.ForeignKey!);

                yield return new BridgeRelationship(bridge, parent, child, entity);
            }
        }

        foreach(var entity in entities)
        {
            var keys = entity.ForeignKeys;
            var bridges = new HashSet<FkAttribute>();

            foreach(var bridge in GetBridges(entity, bridges))
                yield return bridge;

            foreach (var key in keys)
            {
                if (key.IgnoreInRelationship ||
                    bridges.Contains(key)) continue;

                yield return new OneToManyRelationship(key.Type, entity.Type, key);
            }
        }
    }

    public static string FixPath(string path, char joiner, char[]? replacers = null)
    {
        replacers ??= ['\\', '/'];
        return string.Join(joiner, path.Split(replacers));
    }

    public string[] GenerateManifestList(Entities entities, char dirJoiner, string[] before, string[] after)
    {
        var deps = OrderDependencies(entities)
            .SelectMany<IEntity, string>(t =>
            {
                var create = FixPath(t.FileName, dirJoiner);
                if (t is not TypeEntity type || string.IsNullOrEmpty(type.FunctionPath))
                    return [create];

                return [create, FixPath(type.FunctionPath, dirJoiner)];
            });
        return [..before, ..deps, ..after];
    }
}
