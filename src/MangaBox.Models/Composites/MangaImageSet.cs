namespace MangaBox.Models.Composites;

/// <summary>
/// Represents a set of images
/// </summary>
/// <param name="Manga">The manga associated with the images</param>
/// <param name="Sources">The sources of the images</param>
/// <param name="Images">The images themselves</param>
public record class MangaImageSet(
	MbManga[] Manga,
	MbSource[] Sources,
	MbImage[] Images);
