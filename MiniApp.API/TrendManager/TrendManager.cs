using MiniApp.API.Redis;
using StackExchange.Redis;

namespace MiniApp.API.TrendManager
{
    public class GetTrendRankWithTrendValueResponseData
    {
        public GetTrendRankWithTrendValueResponseData(List<string> miniApps, List<double> trendValues)
        {
            this.miniApps = miniApps;
            this.trendValues = trendValues;
        }

        public List<string> miniApps;
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

        // 获取某MiniApp的热度值
        public async Task<double> GetTrendValueById(string id)
        {
            DateTime now = DateTime.Now;

            IDatabase miniAppRedis = _redisConnection.GetMiniAppDatabase();
            double? trendValue = await miniAppRedis.SortedSetScoreAsync($"TrendList{GetCycleSuffix(now)}", id);
            return trendValue ?? 0;
        }

        // 获取一组MiniApp的热度值
        public async Task<List<double>> GetTrendValues(List<string> idList)
        {
            DateTime now = DateTime.Now;

            IDatabase miniAppRedis = _redisConnection.GetMiniAppDatabase();
            List<RedisValue> queryList = new();
            idList.ForEach((id) =>
            {
                queryList.Add(new RedisValue(id));
            });

            double?[] queryResults = await miniAppRedis.SortedSetScoresAsync($"TrendList{GetCycleSuffix(now)}", queryList.ToArray());

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

            IDatabase miniAppRedis = _redisConnection.GetMiniAppDatabase();
            SortedSetEntry[] sortedSetEntries = await miniAppRedis.SortedSetRangeByRankWithScoresAsync($"TrendList{GetCycleSuffix(now)}", start, stop, order: Order.Descending);

            List<string> miniApps = new();
            List<double> trendValues = new();
            sortedSetEntries.ToList().ForEach((value) =>
            {
                miniApps.Add(value.Element!);
                trendValues.Add(value.Score);
            });
            return new(miniApps, trendValues);
        }

        // 当某MiniApp被Open时，触发此热度机制，热度+3
        public async Task OpenAction(string id, int uuid)
        {
            IDatabase miniAppRedis = _redisConnection.GetMiniAppDatabase();

            // 两小时内重复Open不加热度
            if (await miniAppRedis.KeyExistsAsync($"{uuid}opens{id}"))
            {
                return;
            }
            _ = miniAppRedis.StringSetAsync($"{uuid}opens{id}", "", TimeSpan.FromMinutes(120));

            var batch = miniAppRedis.CreateBatch();
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
