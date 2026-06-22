namespace AquaMonitor.API.Data;

public interface IDbConnectionFactory
{
    SqliteConnection Create();
}

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _path;

    public SqliteConnectionFactory(string path)
    {
        _path = path;
    }

    public SqliteConnection Create() => new SqliteConnection(_path);
}
