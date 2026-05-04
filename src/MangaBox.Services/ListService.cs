using MangaDexSharp;

namespace MangaBox.Services;

using Utilities.MangaDex;

using static Constants;

/// <summary>
/// A service for interfacing with <see cref="MbList"/> and <see cref="MbListItem"/>
/// </summary>
public interface IListService
{
	/// <summary>
	/// A request to create a list
	/// </summary>
	/// <param name="request">The request</param>
	/// <param name="profileId">The ID of the profile creating the list</param>
	/// <returns>The created list</returns>
	Task<Boxed> Create(MbList.ListCreate request, Guid? profileId);

	/// <summary>
	/// A request to edit a list
	/// </summary>
	/// <param name="request">The request</param>
	/// <param name="listId">The ID of the list to edit</param>
	/// <param name="profileId">The ID of the profile editing the list</param>
	/// <returns>The updated list</returns>
	Task<Boxed> Edit(MbList.ListUpdate request, Guid listId, Guid? profileId);

	/// <summary>
	/// Fetches a list by it's ID
	/// </summary>
	/// <param name="listId">The ID of the list</param>
	/// <param name="profileId">The ID of the profile requesting the list</param>
	/// <returns>The list if found, otherwise null</returns>
	Task<Boxed> Fetch(Guid listId, Guid? profileId);

	/// <summary>
	/// The request to add a manga to a list
	/// </summary>
	/// <param name="request">The request</param>
	/// <returns>The results of the operation</returns>
	Task<Boxed> Link(MbListItem.LinkRequest request);

	/// <summary>
	/// The request to remove a manga from a list
	/// </summary>
	/// <param name="request">The request</param>
	/// <returns>The results of the operation</returns>
	Task<Boxed> Unlink(MbListItem.LinkRequest request);

	/// <summary>
	/// Imports a list from MD. 
	/// </summary>
	/// <param name="request">The request to import the list</param>
	/// <param name="profileId">The ID of the profile who owns the list</param>
	/// <param name="token">The cancellation token for the request</param>
	/// <returns>The response</returns>
	Task<Boxed> Import(MbList.ListImportMD request, Guid? profileId, CancellationToken token);
}

internal class ListService(
	IDbService _db,
	IMangaDexService _md,
	ISourceService _sources,
	IMangaLoaderService _loader,
	ILogger<ListService> _logger) : IListService
{
	/// <summary>
	/// Cleans the <see cref="MbList.Name"/>
	/// </summary>
	/// <param name="name">The name to clean</param>
	/// <returns>The cleaned name</returns>
	public static string CleanName(string name)
	{
		name = name.Trim().Replace("\n", " ").Replace("\r", "");
		while (name.Contains("  "))
			name = name.Replace("  ", " ");
		return name;
	}

	/// <summary>
	/// Validate the response and return the appropriate result
	/// </summary>
	/// <param name="response">The response to the request</param>
	/// <param name="request">The request that was made</param>
	/// <returns>The result of the validation</returns>
	public async Task<Boxed> Validate(MbListItemResponse? response, MbListItem.LinkRequest request)
	{
		if (response is null)
			return Boxed.Exception("An unexpected error occurred.");

		if (response.Error is null && response.Id is not null)
			return await Fetch(request.ListId, request.ProfileId);

		return response.Error switch
		{
			MbListError.ProfileIdNull => Boxed.Unauthorized(),
			MbListError.ProfileMissing => Boxed.Unauthorized(),
			MbListError.ProfileMismatch => Boxed.NotFound(nameof(MbList)),
			MbListError.ListMissing => Boxed.NotFound(nameof(MbList)),
			MbListError.MangaMissing => Boxed.NotFound(nameof(MbManga)),
			MbListError.ListItemMistmatch => Boxed.Bad("The manga is not in the list."),
			_ => Boxed.Exception("An unexpected error occurred.")
		};
	}

	public async Task<Boxed> Create(MbList.ListCreate request, Guid? profileId)
	{
		if (profileId is null)
			return Boxed.Unauthorized("You must be logged in to create a list.");

		var name = CleanName(request.Name);
		if (string.IsNullOrWhiteSpace(name))
			return Boxed.Bad("Name cannot be empty.");

		if (!string.IsNullOrEmpty(request.Description) && request.Description.Length > MAX_TEXT_LENGTH)
			return Boxed.Bad($"Description cannot be longer than {MAX_TEXT_LENGTH} characters.");

		var list = await _db.List.Fetch(name, profileId.Value);
		if (list is not null)
			return Boxed.Conflict(nameof(MbList));

		list = new()
		{
			ProfileId = profileId.Value,
			Name = name,
			Description = request.Description,
			IsPublic = request.IsPublic
		};
		list.Id = await _db.List.Insert(list);
		await _db.ListExt.Update(list.Id);
		return await Fetch(list.Id, profileId);
	}

	public async Task<Boxed> Edit(MbList.ListUpdate request, Guid listId, Guid? profileId)
	{
		if (profileId is null)
			return Boxed.Unauthorized("You must be logged in to edit a list.");

		if (!string.IsNullOrEmpty(request.Description) && request.Description.Length > MAX_TEXT_LENGTH)
			return Boxed.Bad($"Description cannot be longer than {MAX_TEXT_LENGTH} characters.");

		var list = await _db.List.Fetch(listId);
		if (list is null)
			return Boxed.NotFound(nameof(MbList));

		if (list.ProfileId != profileId.Value)
			return Boxed.Unauthorized("You do not have permission to edit this list.");

		list.Description = request.Description;
		list.IsPublic = request.IsPublic;
		await _db.List.Update(list);
		await _db.ListExt.Update(list.Id);
		return await Fetch(list.Id, profileId);
	}

	public async Task<Boxed> Fetch(Guid listId, Guid? profileId)
	{
		var list = await _db.List.FetchWithRelationships(listId);
		if (list is null || list.Entity is null)
			return Boxed.NotFound(nameof(MbList));

		if (list.Entity.IsPublic)
			return Boxed.Ok(list);

		if (profileId is null)
			return Boxed.Unauthorized("You must be logged in to view this list.");

		if (list.Entity.ProfileId != profileId.Value)
			return Boxed.NotFound(nameof(MbList), "You do not own this list");

		return Boxed.Ok(list);
	}

	public async Task<Boxed> Link(MbListItem.LinkRequest request)
	{
		var result = await _db.ListItem.Create(request);
		await _db.ListExt.Update(request.ListId);
		return await Validate(result, request);
	}

	public async Task<Boxed> Unlink(MbListItem.LinkRequest request)
	{
		var result = await _db.ListItem.Delete(request);
		await _db.ListExt.Update(request.ListId);
		return await Validate(result, request);
	}

	public async Task<Boxed> Import(MbList.ListImportMD request, Guid? profileId, CancellationToken token)
	{
		if (!profileId.HasValue) return Boxed.Unauthorized();

		var mdList = await _md.List(request.MdListId);
		if (mdList is null)
			return Boxed.NotFound("MangaDex List", "The list with the given ID wasn't found");

		if (mdList.IsError(out string error))
			return Boxed.Exception("MangaDex List Fetch Error: " + error);

		var name = CleanName(request.Name?.ForceNull()
			?? mdList.Data?.Attributes?.Name?.ForceNull()
			?? "Imported MD List");

		var description = request.Description?.ForceNull()
			?? $"Imported from a [MangaDex List](https://mangadex.org/list/{request.MdListId})";

		var list = await _db.List.Fetch(name, profileId.Value);
		if (list is not null)
			return Boxed.Conflict(nameof(MbList));

		var mangaIds = mdList.Data.Manga().Select(t => t.Id).Distinct().ToArray();
		if (mangaIds.Length == 0)
			return Boxed.Bad("The MD list is empty");

		var md = await _sources.FindBySlug("mangadex", token);
		if (md is null)
			return Boxed.Exception("MangaDex source not found");

		var found = (await _db.Manga.ByIds(md.Info.Id, mangaIds)).Select(t => t.Entity).ToList();
		var missing = mangaIds.Except(found.Select(t => t.OriginalSourceId)).ToArray();
		var failed = new Dictionary<string, string>();
		foreach(var manga in missing)
		{
			try
			{
				var loaded = await _loader.Load(profileId, $"https://mangadex.org/title/{manga}", false, token);
				if (!loaded.Success || 
					loaded is not Boxed<MangaBoxType<MbManga>> loadedMb ||
					loadedMb?.Data?.Entity is null)
				{
					var errorMessage = loaded.Errors is null || loaded.Errors.Length == 0 
						? "Unknown error" : string.Join("; ", loaded.Errors);
					failed[manga] = $"Failed to load manga: {errorMessage}";
					continue;
				}

				found.Add(loadedMb.Data.Entity);
			}
			catch (Exception ex)
			{
				failed[manga] = $"Failed to load manga due to error: {ex.Message}";
				_logger.LogError(ex, "Failed to load MD manga with ID {MangaId}", manga);
			}
		}

		if (found.Count == 0)
			return Boxed.Bad("None of the manga in the MD list could be imported. Errors: " + 
				string.Join("; ", failed.Select(t => $"Manga {t.Key}: {t.Value}")));

		list = new()
		{
			ProfileId = profileId.Value,
			Name = name,
			Description = description,
			IsPublic = request.IsPublic
		};
		list.Id = await _db.List.Insert(list);

		foreach (var item in found)
			await _db.ListItem.Create(new(list.Id, item.Id, profileId));
		await _db.ListExt.Update(list.Id);
		var data = await _db.List.FetchWithRelationships(list.Id);
		return Boxed.Ok(new MbListImportResponse
		{
			List = data,
			Failures = failed
		});
	}
}
