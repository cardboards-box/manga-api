namespace MangaBox.Api.Middleware;

/// <summary>
/// Wraps the <see cref="ProducesResponseTypeAttribute"/> for the <see cref="Boxed"/> types
/// </summary>
/// <param name="code">The optional status code of the result</param>
public class ProducesBoxAttribute(int code = 200)
    : ProducesResponseTypeAttribute(typeof(Boxed), code)
{ }

/// <summary>
/// Wraps the <see cref="ProducesResponseTypeAttribute"/> for the <see cref="Boxed{T}"/> types
/// </summary>
/// <typeparam name="T">The type of result</typeparam>
/// <param name="code">The optional status code of the result</param>
public class ProducesBoxAttribute<T>(int code = 200)
    : ProducesResponseTypeAttribute(typeof(Boxed<T>), code)
{ }

/// <summary>
/// Wraps the <see cref="ProducesResponseTypeAttribute"/> for the <see cref="BoxedArray{T}"/> types
/// </summary>
/// <typeparam name="T">The type of result</typeparam>
/// <param name="code">The optional status code of the result</param>
public class ProducesArrayAttribute<T>(int code = 200)
    : ProducesResponseTypeAttribute(typeof(BoxedArray<T>), code)
{ }

/// <summary>
/// Wraps the <see cref="ProducesResponseTypeAttribute"/> for the <see cref="BoxedPaged{T}"/> types
/// </summary>
/// <typeparam name="T">The type of result</typeparam>
/// <param name="code">The optional status code of the result</param>
public class ProducesPagedAttribute<T>(int code = 200)
    : ProducesResponseTypeAttribute(typeof(BoxedPaged<T>), code)
{ }

/// <summary>
/// Wraps the <see cref="ProducesResponseTypeAttribute"/> for the <see cref="BoxedError"/> types
/// </summary>
/// <param name="code">The optional status code of the result</param>
public class ProducesErrorAttribute(int code = 500)
    : ProducesResponseTypeAttribute(typeof(BoxedError), code)
{ }