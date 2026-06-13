namespace MangaBox.Database.Services;

using Models;
using Models.Composites;
using Models.Composites.Filters;

/// <summary>
/// The service for interacting with the mb_profiles table
/// </summary>
public interface IMbProfileDbService
{
    /// <summary>
    /// Fetches a record by its ID from the mb_profiles table
    /// </summary>
    /// <param name="id">The ID of the record</param>
    /// <returns>The record</returns>
    Task<MbProfile?> Fetch(Guid id);

    /// <summary>
    /// Inserts a record into the mb_profiles table
    /// </summary>
    /// <param name="item">The item to insert</param>
    /// <returns>The ID of the inserted record</returns>
    Task<Guid> Insert(MbProfile item);

    /// <summary>
    /// Updates a record in the mb_profiles table
    /// </summary>
    /// <param name="item">The record to update</param>
    /// <returns>The number of records updated</returns>
    Task<int> Update(MbProfile item);

    /// <summary>
    /// Inserts a record in the mb_profiles table if it doesn't exist, otherwise updates it
    /// </summary>
    /// <param name="item">The item to update or insert</param>
    /// <returns>The ID of the inserted/updated record</returns>
    Task<Guid> Upsert(MbProfile item);

    /// <summary>
    /// Gets all of the records from the mb_profiles table
    /// </summary>
    /// <returns>All of the records</returns>
    Task<MbProfile[]> Get();

    /// <summary>
    /// Fetches the record and all related records
    /// </summary>
    /// <param name="id">The ID of the record to fetch</param>
    /// <returns>The record and all related records</returns>
    Task<MangaBoxType<MbProfile>?> FetchWithRelationships(Guid id);

    /// <summary>
    /// Gets a list of all of the admins
    /// </summary>
    /// <returns>The admins</returns>
    Task<MbProfile[]> Admins();

    /// <summary>
    /// Update the settings for the given profile
    /// </summary>
    /// <param name="profileId">The ID of the profile</param>
    /// <param name="settings">The settings blob</param>
    /// <returns>The profile that was updated</returns>
    Task<MbProfile?> Settings(Guid profileId, string? settings);

	/// <summary>
	/// Update the notification settings for the given profile
	/// </summary>
	/// <param name="profileId">The ID of the profile</param>
	/// <param name="settings">The notification settings</param>
	/// <returns>The profile that was updated</returns>
	Task<MbProfile?> Notifications(Guid profileId, MbProfile.ProfileNotifications settings);

	/// <summary>
	/// Searches profiles by the given filter
	/// </summary>
	/// <param name="filter">The search filter</param>
	/// <returns>The profiles</returns>
	Task<PaginatedResult<MbProfile>> Search(ProfileSearchFilter filter);

	/// <summary>
	/// Gets all distinct profile providers
	/// </summary>
	/// <returns>The providers</returns>
	Task<string[]> Providers();

	/// <summary>
	/// Updates whether or not the profile is approved to read
	/// </summary>
	/// <param name="profileId">The profile ID</param>
	/// <param name="canRead">Whether or not the profile is approved to read</param>
	/// <returns>The profile that was updated</returns>
	Task<MbProfile?> CanRead(Guid profileId, bool canRead);

	/// <summary>
	/// Updates whether or not the profile is an administrator
	/// </summary>
	/// <param name="profileId">The profile ID</param>
	/// <param name="admin">Whether or not the profile is an administrator</param>
	/// <returns>The profile that was updated</returns>
	Task<MbProfile?> Admin(Guid profileId, bool admin);
}

internal class MbProfileDbService(
    IOrmService orm) : Orm<MbProfile>(orm), IMbProfileDbService
{
    private static string? _queryAdmins;

    public Task<MbProfile[]> Admins()
    {
        _queryAdmins ??= Map.Select(t => t.With(a => a.Admin));
        return Get(_queryAdmins, new { Admin = true });
	}

    public Task<MbProfile?> Settings(Guid profileId, string? settings)
    {
        const string QUERY = @"
UPDATE mb_profiles 
SET settings_blob = :settings 
WHERE 
    id = :profileId AND
    deleted_at IS NULL;

SELECT * 
FROM mb_profiles 
WHERE 
    id = :profileId AND
    deleted_at IS NULL;";
        return Fetch(QUERY, new { profileId, settings });
    }

    public Task<MbProfile?> Notifications(Guid profileId, MbProfile.ProfileNotifications settings)
    {
        const string QUERY = """
            UPDATE mb_profiles
            SET
                notify_favourites = :favourites,
                notify_in_progress = :inProgress
            WHERE
                id = :profileId AND
                deleted_at IS NULL;

            SELECT * 
            FROM mb_profiles 
            WHERE 
                id = :profileId AND
                deleted_at IS NULL;
            """;

        return Fetch(QUERY, new
        {
            profileId,
            favourites = settings.Favourites,
            inProgress = settings.InProgress
        });
    }

	public async Task<PaginatedResult<MbProfile>> Search(ProfileSearchFilter filter)
	{
		var query = filter.Build(out var pars);
		using var con = await _sql.CreateConnection();
		using var rdr = await con.QueryMultipleAsync(query, pars);

		var total = await rdr.ReadSingleAsync<int>();
		if (total == 0) return new();
		var size = filter.Size <= 0 ? 100 : filter.Size;
		var pages = (int)Math.Ceiling((double)total / size);

		var results = await rdr.ReadAsync<MbProfile>();
		return new(pages, total, [..results]);
	}

	public async Task<string[]> Providers()
	{
		const string QUERY = """
            SELECT DISTINCT LOWER(provider)
            FROM mb_profiles
            WHERE
                provider IS NOT NULL AND
                provider <> '' AND
                deleted_at IS NULL
            ORDER BY provider;
            """;

		using var con = await _sql.CreateConnection();
		var providers = await con.QueryAsync<string>(QUERY);
		return [..providers];
	}

	public Task<MbProfile?> CanRead(Guid profileId, bool canRead)
	{
		const string QUERY = """
			UPDATE mb_profiles
			SET can_read = :canRead
			WHERE
				id = :profileId AND
				deleted_at IS NULL;

			SELECT *
			FROM mb_profiles
			WHERE
				id = :profileId AND
				deleted_at IS NULL;
			""";

		return Fetch(QUERY, new { profileId, canRead });
	}

	public Task<MbProfile?> Admin(Guid profileId, bool admin)
	{
		const string QUERY = """
			UPDATE mb_profiles
			SET admin = :admin
			WHERE
				id = :profileId AND
				deleted_at IS NULL;

			SELECT *
			FROM mb_profiles
			WHERE
				id = :profileId AND
				deleted_at IS NULL;
			""";

		return Fetch(QUERY, new { profileId, admin });
	}

    public async Task<MangaBoxType<MbProfile>?> FetchWithRelationships(Guid id)
    {
        const string QUERY = @"SELECT * 
FROM mb_profiles 
WHERE 
    id = :id AND 
    deleted_at IS NULL;
";
        using var con = await _sql.CreateConnection();
        using var rdr = await con.QueryMultipleAsync(QUERY, new { id });

        var item = await rdr.ReadSingleOrDefaultAsync<MbProfile>();
        if (item is null) return null;

        var related = new List<MangaBoxRelationship>();


        return new MangaBoxType<MbProfile>(item, [..related]);
    }
}
