using LoxSmoke.DocXml;

namespace MangaBox.Database.Generation.TypeGeneration;

/// <summary>
/// Service for getting intellisense comments from types
/// </summary>
public interface ICommentsService
{
    /// <summary>
    /// Gets the comments of the given type
    /// </summary>
    /// <param name="type">The type to fetch the comments for</param>
    /// <returns>The type information</returns>
    TypeComments ByType(Type type);
}

internal class CommentsService : ICommentsService
{
    private string? _nuGetLocation;
    private DocXmlReader? _reader;
    private readonly Dictionary<Type, TypeComments> _types = [];

    /// <summary>
    /// Gets the location of the NuGet packages folder to load types from third party libraries
    /// </summary>
    /// <returns>The path to the NuGet packages cache</returns>
    public string? GetNuGetPath()
    {
        if (!string.IsNullOrEmpty(_nuGetLocation)) return _nuGetLocation;

        var location = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(location, ".nuget", "packages");
        if (Directory.Exists(path)) return _nuGetLocation = path;

        return null;
    }

    /// <summary>
    /// Gets the path to the XML documentation file for a given assembly
    /// </summary>
    /// <param name="assembly">The assembly to get the path for</param>
    /// <returns>The path to the XML documentation file (if it doesn't exist, it's null)</returns>
    public string? GetPath(Assembly assembly)
    {
        var path = Path.ChangeExtension(assembly.Location, ".xml");
        if (File.Exists(path)) return path;

        var fileName = Path.GetFileName(path);

        var nuGet = GetNuGetPath();
        if (nuGet is null) return null;

        var files = Directory.GetFiles(nuGet, fileName, SearchOption.AllDirectories);
        return files.OrderByDescending(t => t).FirstOrDefault();
    }

    /// <summary>
    /// Gets all of the assemblies that are loaded in the current process
    /// </summary>
    /// <param name="current">The current assembly to load</param>
    /// <param name="loaded">The assemblies that have been loaded</param>
    /// <returns>All of the loaded assemblies</returns>
    public static IEnumerable<Assembly> GetAllAssemblies(
        Assembly? current = null, 
        HashSet<AssemblyName>? loaded = null)
    {
        loaded ??= [];
        current ??= Assembly.GetEntryAssembly();
        if (current is null) yield break;

        yield return current;

        var references = current.GetReferencedAssemblies();
        foreach(var assembly in references)
        {
            if (loaded.Contains(assembly)) continue;

            loaded.Add(assembly);

            var asm = Assembly.Load(assembly);
            if (asm is null) continue;

            foreach (var reference in GetAllAssemblies(asm, loaded))
                yield return reference;
        }
    }

    /// <summary>
    /// Gets the document reader for all of the loaded assemblies
    /// </summary>
    /// <returns>The document reader</returns>
    public DocXmlReader GetReader() => 
        _reader ??= new DocXmlReader(GetAllAssemblies(), GetPath);

    /// <summary>
    /// Gets the property comments for the given property
    /// </summary>
    /// <param name="parent">The parent type</param>
    /// <param name="info">The property</param>
    /// <param name="reader">The document comment reader</param>
    /// <returns>The comments for the property</returns>
    public PropertyComments ByProperty(Type parent, PropertyInfo info, DocXmlReader? reader = null)
    {
        reader ??= GetReader();
        var xml = reader.GetMemberComments(info);
        var type = ByType(info.PropertyType);

        return new PropertyComments(
            parent,
            info,
            type,
            xml.Summary?.ForceNull(),
            xml.Remarks?.ForceNull(),
            xml.Example?.ForceNull());
    }

    public TypeComments ByType(Type type)
    {
        if (_types.TryGetValue(type, out var comments))
            return comments;

        var reader = GetReader();
        var xml = reader.GetTypeComments(type);
        comments = new TypeComments(
            type,
            xml.Summary?.ForceNull(),
            xml.Remarks?.ForceNull(),
            xml.Example?.ForceNull());
        _types.Add(type, comments);

        var properties = new List<PropertyComments>();
        foreach (var property in type.GetProperties())
            properties.Add(ByProperty(type, property, reader));

        comments.Properties = [.. properties];
        return comments;
    }
}
