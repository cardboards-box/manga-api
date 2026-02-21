using HtmlAgilityPack;

namespace MangaBox.Utilities.Flare;

using Models;

/// <summary>
/// An HTML document with additional properties for Flare operations
/// </summary>
public class FlareHtmlDocument : HtmlDocument
{
	/// <summary>
	/// The solution used to obtain this document
	/// </summary>
	public required SolverSolution FlareSolution { get; set; }
}
