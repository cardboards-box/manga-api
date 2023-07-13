using Npgsql;

namespace CardboardBox.Core;

public class SqlConfig : ISqlConfig<NpgsqlConnection>
{
    private readonly IConfiguration _config;

    public string ConnectionString => 
        _config["Database:ConnectionString"] 
            ?? throw new ArgumentNullException("Database:ConnectionString", "Required setting is not present");

    public int Timeout => int.TryParse(_config["Database:Timeout"], out int timeout) ? timeout : 0;

    public SqlConfig(IConfiguration config)
    {
        _config = config;
    }
}
