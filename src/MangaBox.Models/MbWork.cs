namespace MangaBox.Models;

/// <summary>
/// A roll up for the same manga that are available on different sources
/// </summary>
[Table("mb_works")]
[InterfaceOption(nameof(MbWork))]
public class MbWork : MbDbObject
{

}
