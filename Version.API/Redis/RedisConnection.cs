using StackExchange.Redis;

namespace Version.API.Redis
{
    public class RedisConnection
    {
        private readonly IConfiguration _configuration;
        private readonly ConnectionMultiplexer _connectionMultiplexer;

        public RedisConnection(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionMultiplexer = ConnectionMultiplexer.Connect(_configuration.GetConnectionString("Redis")!);
        }

        public IDatabase GetVersionDatabase()
        {
            return _connectionMultiplexer.GetDatabase(int.Parse(_configuration["RedisDatabase:Version"]!));
        }
    }
}
