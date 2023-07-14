namespace CardboardBox.Manga.ReverseImageSearch.GoogleVision;

public record class VisionResults(string Guess, float Score, (string Url, string Title)[] WebPages);
