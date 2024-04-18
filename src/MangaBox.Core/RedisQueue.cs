namespace MangaBox.Core;

public interface IRedisQueue
{
    Task Queue<T>(string name, T item);
}

public class RedisQueue(IRedisService _redis) : IRedisQueue
{
    public async Task Queue<T>(string name, T item)
    {
        await _redis.List<T>(name).Append(item);
        await _redis.Publish(name, item);
    }
}
