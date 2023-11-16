using Feed.API.Redis;
using StackExchange.Redis;

namespace Feed.API.TrendManager
{
    public class GetTrendRankWithTrendValueResponseData
    {
        public GetTrendRankWithTrendValueResponseData(List<string> feeds, List<double> trendValues)
        {
            this.feeds = feeds;
            this.trendValues = trendValues;
        }

        public List<string> feeds;
        public List<double> trendValues;
    }

    public class TrendManager
    {
        //依赖注入
        private readonly RedisConnection _redisConnection;

        public TrendManager(RedisConnection redisConnection)
        {
            _redisConnection = redisConnection;
        }

        // 传入DateTime，根据DateTime计算出对应的周期后缀
        // 计算规则为
        // ①：按两小时作为周期，从 当日 00:00 PM 起至 当日 23:59 PM 结束，一天共划分12个周期。
        // ②：XXXX年XX月XX日00:00PM - 01:59PM 后缀计算为：XXXX-XX-XX-01
        // ③：XXXX年XX月XX日02:00PM - 03:59PM 后缀计算为：XXXX-XX-XX-02
        // ④：依此类推，XXXX年XX月XX日22:00PM - 23:59PM 后缀计算为：XXXX-XX-XX-12
        private string GetCycleSuffix(DateTime dateTime)
        {
            return $"{dateTime.Year}-{dateTime.Month}-{dateTime.Day}-{(dateTime.Hour / 2) + 1}";
        }

        // 获取某Feed的热度值
        public async Task<double> GetTrendValueById(string id)
        {
            DateTime now = DateTime.Now;

            IDatabase feedRedis = _redisConnection.GetFeedDatabase();
            double? trendValue = await feedRedis.SortedSetScoreAsync($"TrendList{GetCycleSuffix(now)}", id);
            return trendValue ?? 0;
        }

        // 获取一组Feed的热度值
        public async Task<List<double>> GetTrendValues(List<string> idList)
        {
            DateTime now = DateTime.Now;

            IDatabase feedRedis = _redisConnection.GetFeedDatabase();
            List<RedisValue> queryList = new();
            idList.ForEach((id) =>
            {
                queryList.Add(new RedisValue(id));
            });

            double?[] queryResults = await feedRedis.SortedSetScoresAsync($"TrendList{GetCycleSuffix(now)}", queryList.ToArray());

            List<double> results = new();
            foreach (double? trendValue in queryResults)
            {
                results.Add(trendValue ?? 0);
            };

            return results;

        }

        // 获取当前热度排行榜中的一段数据（包含热度值）
        public async Task<GetTrendRankWithTrendValueResponseData> GetTrendRankWithTrendValueByRange(int start, int stop)
        {
            DateTime now = DateTime.Now;

            IDatabase feedRedis = _redisConnection.GetFeedDatabase();
            SortedSetEntry[] sortedSetEntries = await feedRedis.SortedSetRangeByRankWithScoresAsync($"TrendList{GetCycleSuffix(now)}", start, stop, order: Order.Descending);

            List<string> feeds = new();
            List<double> trendValues = new();
            sortedSetEntries.ToList().ForEach((value) =>
            {
                feeds.Add(value.Element!);
                trendValues.Add(value.Score);
            });
            return new(feeds, trendValues);
        }

        // 当某Feed被Read时，触发此热度机制，热度+3
        public async Task ReadAction(string id, int uuid)
        {
            IDatabase feedRedis = _redisConnection.GetFeedDatabase();

            // 两小时内重复Read不加热度
            if (await feedRedis.KeyExistsAsync($"{uuid}reads{id}"))
            {
                return;
            }
            _ = feedRedis.StringSetAsync($"{uuid}reads{id}", "", TimeSpan.FromMinutes(120));

            var batch = feedRedis.CreateBatch();
            DateTime now = DateTime.Now;
            DateTime next = now.AddHours(2);

            double trendValue = 3.0;

            _ = batch.SortedSetIncrementAsync($"TrendList{GetCycleSuffix(now)}", id, trendValue);
            _ = batch.SortedSetIncrementAsync($"TrendCycle{GetCycleSuffix(now)}", id, trendValue);
            _ = batch.SortedSetIncrementAsync($"TrendList{GetCycleSuffix(next)}", id, trendValue);

            batch.Execute();
        }
    }
}
