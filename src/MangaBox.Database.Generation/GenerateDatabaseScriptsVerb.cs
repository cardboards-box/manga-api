namespace MangaBox.Database.Generation;

using Models;
using Services;

/// <summary>
/// Generate the model creation scripts for the database models
/// </summary>
[Verb("generate-database-scripts", HelpText = "Generate the model creation scripts for the database models")]
public class GenerateDatabaseScriptsOptions
{
    /// <summary>
    /// The default manifest file name
    /// </summary>
    public const string DEFAULT_MANIFEST = Constants.APPLICATION_NAME + ".manifest.json";

    /// <summary>
    /// The default directory to write the scripts to
    /// </summary>
    public const string DEFAULT_DIRECTORY = "Generated\\Scripts";

    /// <summary>
    /// The default directory to write the table scripts to
    /// </summary>
    public const string DEFAULT_TABLE_DIR = "Tables";

    /// <summary>
    /// The default directory to write the type scripts to
    /// </summary>
    public const string DEFAULT_TYPE_DIR = "Types";

    /// <summary>
    /// The default directory name used to store function files.
    /// </summary>
    public const string DETAULT_FUNC_DIR = "Functions";

    /// <summary>
    /// The default files to include before the generated files in the manifest
    /// </summary>
    public static readonly string[] DEFAULT_INCLUDE_BEFORE = ["extensions.sql", "Functions/text_array_join.sql"];

    /// <summary>
    /// The defaulte files to include after the generated files in the manifest
    /// </summary>
    public static readonly string[] DEFAULT_INCLUDE_AFTER = [];

    /// <summary>
    /// The directory to write the scripts to
    /// </summary>
    [Option('d', "directory", HelpText = "The directory to write the scripts to", Default = DEFAULT_DIRECTORY)]
    public string Directory { get; set; } = DEFAULT_DIRECTORY;

    /// <summary>
    /// The prefix to use for all files
    /// </summary>
    [Option('p', "prefix", HelpText = "The prefix to use for all files")]
    public string? Prefix { get; set; }

    /// <summary>
    /// The name of the manifest file to generate
    /// </summary>
    [Option('m', "manifest", HelpText = "The name of the manifest file to generate", Default = DEFAULT_MANIFEST)]
    public string Manifest { get; set; } = DEFAULT_MANIFEST;

    /// <summary>
    /// The name of the directory to create tables in
    /// </summary>
    [Option('t', "table-dir", HelpText = "The name of the directory to create tables in", Default = DEFAULT_TABLE_DIR)]
    public string TableDir { get; set; } = DEFAULT_TABLE_DIR;

    /// <summary>
    /// The name of the directory to create types in
    /// </summary>
    [Option('y', "type-dir", HelpText = "The name of the directory to create types in", Default = DEFAULT_TYPE_DIR)]
    public string TypeDir { get; set; } = DEFAULT_TYPE_DIR;

    /// <summary>
    /// The name of the directory in which to create functions.
    /// </summary>
    [Option('f', "func-dir", HelpText = "The name of the directory to create functions in", Default = DETAULT_FUNC_DIR)]
    public string FuncDir { get; set; } = DETAULT_FUNC_DIR;

    /// <summary>
    /// The version of the application to generate the scripts for (default includes all columns)
    /// </summary>
    [Option('v', "as-of-version", HelpText = "The version of the application to generate the scripts for (default includes all columns)")]
    public int? AsOfVersion { get; set; }

    /// <summary>
    /// Files to include before the generated files in the manifest
    /// </summary>
    [Option('b', "include-before", HelpText = "Files to include before the generated files in the manifest", Default = null)]
    public IEnumerable<string>? IncludeBefore { get; set; } = null;

    /// <summary>
    /// Files to include after the generated files in the manifest
    /// </summary>
    [Option('a', "include-after", HelpText = "Files to include after the generated files in the manifest", Default = null)]
    public IEnumerable<string>? IncludeAfter { get; set; } = null;
}

internal class GenerateDatabaseScriptsVerb(
    ILogger<GenerateDatabaseScriptsVerb> logger,
    IDatabaseMetadataService _metadata,
    IFileGenerationService _generation) : BooleanVerb<GenerateDatabaseScriptsOptions>(logger)
{
    private readonly JsonSerializerOptions _indentedOptions = new()
    {
        WriteIndented = true
    };

    public Entities GetEntities(string directory, string tableDir, string typeDir, string funcDir, string? prefix)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogInformation("Creating root scripts directory: {dir}", directory);
            Directory.CreateDirectory(directory);
        }

        var tableDirFull = Path.Combine(directory, tableDir);
        if (!Directory.Exists(tableDirFull))
        {
            _logger.LogInformation("Creating table scripts directory: {dir}", tableDirFull);
            Directory.CreateDirectory(tableDirFull);
        }

        var typeDirFull = Path.Combine(directory, typeDir);
        if (!Directory.Exists(typeDirFull))
        {
            _logger.LogInformation("Creating type scripts directory: {dir}", typeDirFull);
            Directory.CreateDirectory(typeDirFull);
        }

        var funcDirFull = Path.Combine(directory, funcDir);
        if (!Directory.Exists(funcDirFull))
        {
            _logger.LogInformation("Creating function scripts directory: {dir}", funcDirFull);
            Directory.CreateDirectory(funcDirFull);
        }

        return _metadata.GetEntities(tableDir, typeDir, funcDir, prefix);
    }

    public async Task CreateTableScripts(Entities entities, string dir, int asOf)
    {
        var tables = entities.Tables;
        var output = new Dictionary<Type, TableEntity>();

        foreach (var table in tables)
        {
            if (output.ContainsKey(table.Type)) continue;

            output.Add(table.Type, table);
            var filePath = Path.Combine(dir, table.FileName);
            _logger.LogInformation("Creating table script: {table} >> {file}", table.Name, filePath);
            await using var writer = new StreamWriter(filePath);
            await _generation.TableCreate(table, entities, writer, asOf);
        }
    }

    public async Task CreateTypeScripts(Entities entities, string dir, int asOf)
    {
        var types = entities.Types;
        var output = new Dictionary<Type, TypeEntity>();

        foreach (var type in types)
        {
            if (output.ContainsKey(type.Type)) continue;

            output.Add(type.Type, type);
            var filePath = Path.Combine(dir, type.FileName);
            _logger.LogInformation("Creating type script: {type} >> {file}", type.Name, filePath);
            await using var writer = new StreamWriter(filePath);
            await _generation.TypeCreate(type, entities, writer, asOf);
        }
    }

    public async Task CreateArrayFunctions(Entities entities, string dir)
    {
        var types = entities.Types.Where(t => t.ArrayJoin is not null && !string.IsNullOrEmpty(t.FunctionPath));
        foreach (var type in types)
        {
            var join = type.ArrayJoin!.FullName;
            var path = Path.Combine(dir, type.FunctionPath!);
            _logger.LogInformation("Creating array function script: {type} >> {file}", type.Name, path);
            await using var writer = new StreamWriter(path);
            await _generation.ArrayJoinCreate(type, entities, writer);
        }
    }

    public async Task<bool> CreateManifest(Entities entities, string file, string[] before, string[] after)
    {
        var dir = Path.GetDirectoryName(file);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            _logger.LogInformation("Creating manifest directory: {dir}", dir);
            Directory.CreateDirectory(dir);
        }

        var files = _metadata.GenerateManifestList(entities, '/', before, after);
        if (files.Length == 0) return false;

        await using var io = File.Create(file);
        var manifest = new Manifest { Paths = files };
        await JsonSerializer.SerializeAsync(io, manifest, _indentedOptions);
        _logger.LogInformation("Manifest file created: {file}", file);

        return true;
    }

    public override async Task<bool> Execute(GenerateDatabaseScriptsOptions options, CancellationToken token)
    {
        var directory = Path.GetFullPath(options.Directory);
        if (string.IsNullOrEmpty(directory))
        {
            _logger.LogError("Invalid directory specified");
            return false;
        }

        var entities = GetEntities(
            directory, options.TableDir,
            options.TypeDir, options.FuncDir,
            options.Prefix.ForceNull());
        var asOf = options.AsOfVersion ?? int.MaxValue;

        _logger.LogInformation("Starting to create table scripts");
        await CreateTableScripts(entities, directory, asOf);
        _logger.LogInformation("Finished creating table scripts");

        _logger.LogInformation("Starting to create type scripts");
        await CreateTypeScripts(entities, directory, asOf);
        _logger.LogInformation("Finished creating type scripts");

        _logger.LogInformation("Starting to create array function scripts");
        await CreateArrayFunctions(entities, directory);
        _logger.LogInformation("Finished creating array function scripts");

        await using var drop = new StreamWriter(Path.Combine(directory, "drop_tables.sql"));
        await _generation.DropTables(entities, drop);

        var manifestFile = Path.Combine(directory, options.Manifest);
        _logger.LogInformation("Creating manifest file: {file}", manifestFile);

        var befores = options.IncludeBefore?.ToArray() ?? [];
        if (befores.Length == 0)
            befores = GenerateDatabaseScriptsOptions.DEFAULT_INCLUDE_BEFORE;
        var afters = options.IncludeAfter?.ToArray() ?? [];
        if (afters.Length == 0)
            afters = GenerateDatabaseScriptsOptions.DEFAULT_INCLUDE_AFTER;
        if (!await CreateManifest(entities, manifestFile, befores, afters))
            return false;

        _logger.LogInformation("Finished creating manifest file");
        return true;
    }
}
