namespace MangaBox.Services;

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
}

internal class ListService(
	IDbService _db) : IListService
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
		return await Validate(result, request);
	}

	public async Task<Boxed> Unlink(MbListItem.LinkRequest request)
	{
		var result = await _db.ListItem.Delete(request);
		return await Validate(result, request);
	}
}
