using Consul;
using Message.API.DataCollection;
using Message.API.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Message.API.MongoDBServices
{
    public class StickerSeriesDataForClient 
    {
        public StickerSeriesDataForClient(string id, string preview, string tittle)
        {
            Id = id;
            Preview = preview;
            Tittle = tittle;
        }

        public string Id { get; set; }
        public string Preview { get; set; }
        public string Tittle { get; set; }
    }

    public class StickerSeriesService
    {
        private readonly IMongoCollection<StickerSeries> _stickerSeriesDocumentsCollection;

        public StickerSeriesService(IOptions<StickerSeriesCollectionSettings> stickerSeriesCollectionSettings)
        {
            var mongoClient = new MongoClient(
                stickerSeriesCollectionSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                stickerSeriesCollectionSettings.Value.DatabaseName);

            _stickerSeriesDocumentsCollection = mongoDatabase.GetCollection<StickerSeries>(
                stickerSeriesCollectionSettings.Value.StickerSeriesCollectionName);
        }

        public async Task<List<StickerSeriesDataForClient>> GetAllStickerSeries()
        {
            return await _stickerSeriesDocumentsCollection
                .Find(series => series.Id != null)
                .Project(series => new StickerSeriesDataForClient(series.Id!, series.Preview, series.Tittle))
                .ToListAsync();
        }

        public List<Sticker> GetStickersByRange(string stickerSeriesId, int start, int count)
        {
            var series = _stickerSeriesDocumentsCollection
                .Find(series => series.Id == stickerSeriesId)
                .SingleOrDefault();

            if (series == null)
            {
                return new();
            }
            else
            {
                return series.Stickers
                    .Skip(start)
                    .Take(count)
                    .ToList();
            }
        }

        //public void insert()
        //{
        //    _stickerSeriesDocumentsCollection.InsertOneAsync(new(null, "http://10.0.2.2:9000/sticker/ac/ac_喝茶_png.png", "AC娘", new() 
        //    {
        //        new("ac_喝茶","http://10.0.2.2:9000/sticker/ac/ac_喝茶_png.png","喝茶","【<sticker>ac_喝茶_png</sticker>】"),
        //        new("ac_哈哈","http://10.0.2.2:9000/sticker/ac/ac_哈哈_png.png","哈哈","【<sticker>ac_哈哈_png</sticker>】"),
        //        new("ac_哼", "http://10.0.2.2:9000/sticker/ac/ac_哼_png.png", "哼", "【<sticker>ac_哼_png</sticker>】"),
        //        new("ac_大哭", "http://10.0.2.2:9000/sticker/ac/ac_大哭_png.png", "大哭", "【<sticker>ac_大哭_png</sticker>】"),
        //        new("ac_咩哈哈", "http://10.0.2.2:9000/sticker/ac/ac_咩哈哈_png.png", "咩哈哈", "【<sticker>ac_咩哈哈_png</sticker>】"),
        //        new("ac_呵呵", "http://10.0.2.2:9000/sticker/ac/ac_呵呵_png.png", "呵呵", "【<sticker>ac_呵呵_png</sticker>】"),
        //        new("ac_和谐", "http://10.0.2.2:9000/sticker/ac/ac_和谐_png.png", "和谐", "【<sticker>ac_和谐_png</sticker>】"),
        //        new("ac_就这？", "http://10.0.2.2:9000/sticker/ac/ac_就这？_png.png", "就这？", "【<sticker>ac_就这？_png</sticker>】"),
        //        new("ac_尴尬", "http://10.0.2.2:9000/sticker/ac/ac_尴尬_png.png", "尴尬", "【<sticker>ac_尴尬_png</sticker>】"),
        //        new("ac_指指点点", "http://10.0.2.2:9000/sticker/ac/ac_指指点点_png.png", "指指点点", "【<sticker>ac_指指点点_png</sticker>】"),
        //        new("ac_干杯（左）", "http://10.0.2.2:9000/sticker/ac/ac_干杯（左）_png.png", "干杯（左）", "【<sticker>ac_干杯（左）_png</sticker>】"),
        //        new("ac_干杯（右）", "http://10.0.2.2:9000/sticker/ac/ac_干杯（右）_png.png", "干杯（右）", "【<sticker>ac_干杯（右）_png</sticker>】"),
        //        new("ac_啊？", "http://10.0.2.2:9000/sticker/ac/ac_啊？_png.png", "啊？", "【<sticker>ac_啊？_png</sticker>】"),
        //        new("ac_呵", "http://10.0.2.2:9000/sticker/ac/ac_呵_png.png", "呵", "【<sticker>ac_呵_png</sticker>】"),
        //        new("ac_就这", "http://10.0.2.2:9000/sticker/ac/ac_就这_png.png", "就这", "【<sticker>ac_就这_png</sticker>】"),
        //        new("ac_愣住", "http://10.0.2.2:9000/sticker/ac/ac_愣住_png.png", "愣住", "【<sticker>ac_愣住_png</sticker>】"),
        //        new("ac_惊讶", "http://10.0.2.2:9000/sticker/ac/ac_惊讶_png.png", "惊讶", "【<sticker>ac_惊讶_png</sticker>】"),
        //        new("ac_狗头", "http://10.0.2.2:9000/sticker/ac/ac_狗头_png.png", "狗头", "【<sticker>ac_狗头_png</sticker>】"),
        //        new("ac_撒花", "http://10.0.2.2:9000/sticker/ac/ac_撒花_png.png", "撒花", "【<sticker>ac_撒花_png</sticker>】"),
        //        new("ac_瞌睡", "http://10.0.2.2:9000/sticker/ac/ac_瞌睡_png.png", "瞌睡", "【<sticker>ac_瞌睡_png</sticker>】"),
        //        new("ac_扶额", "http://10.0.2.2:9000/sticker/ac/ac_扶额_png.png", "扶额", "【<sticker>ac_扶额_png</sticker>】"),
        //        new("ac_憋笑", "http://10.0.2.2:9000/sticker/ac/ac_憋笑_png.png", "憋笑", "【<sticker>ac_憋笑_png</sticker>】"),
        //        new("ac_我在听", "http://10.0.2.2:9000/sticker/ac/ac_我在听_png.png", "我在听", "【<sticker>ac_我在听_png</sticker>】"),
        //        new("ac_挺好颜", "http://10.0.2.2:9000/sticker/ac/ac_挺好颜_png.png", "挺好颜", "【<sticker>ac_挺好颜_png</sticker>】"),
        //        new("ac_怒斥", "http://10.0.2.2:9000/sticker/ac/ac_怒斥_png.png", "怒斥", "【<sticker>ac_怒斥_png</sticker>】"),
        //        new("ac_欸嘿", "http://10.0.2.2:9000/sticker/ac/ac_欸嘿_png.png", "欸嘿", "【<sticker>ac_欸嘿_png</sticker>】"),
        //        new("ac_烦恼", "http://10.0.2.2:9000/sticker/ac/ac_烦恼_png.png", "烦恼", "【<sticker>ac_烦恼_png</sticker>】"),
        //        new("ac_搞快点", "http://10.0.2.2:9000/sticker/ac/ac_搞快点_png.png", "搞快点", "【<sticker>ac_搞快点_png</sticker>】"),
        //        new("ac_赞", "http://10.0.2.2:9000/sticker/ac/ac_赞_png.png", "赞", "【<sticker>ac_赞_png</sticker>】"),
        //        new("ac_赞啊", "http://10.0.2.2:9000/sticker/ac/ac_赞啊_png.png", "赞啊", "【<sticker>ac_赞啊_png</sticker>】"),
        //        new("ac_秧歌Star", "http://10.0.2.2:9000/sticker/ac/ac_秧歌Star_png.png", "秧歌Star", "【<sticker>ac_秧歌Star_png</sticker>】"),
        //        new("ac_碇司令", "http://10.0.2.2:9000/sticker/ac/ac_碇司令_png.png", "碇司令", "【<sticker>ac_碇司令_png</sticker>】"),
        //        new("ac_强者", "http://10.0.2.2:9000/sticker/ac/ac_强者_png.png", "强者", "【<sticker>ac_强者_png</sticker>】"),
        //        new("ac_尔康", "http://10.0.2.2:9000/sticker/ac/ac_尔康_png.png", "尔康", "【<sticker>ac_尔康_png</sticker>】"),
        //        new("ac_异议", "http://10.0.2.2:9000/sticker/ac/ac_异议_png.png", "异议", "【<sticker>ac_异议_png</sticker>】"),
        //        new("ac_呆住", "http://10.0.2.2:9000/sticker/ac/ac_呆住_png.png", "呆住", "【<sticker>ac_呆住_png</sticker>】"),
        //        new("ac_呃呃", "http://10.0.2.2:9000/sticker/ac/ac_呃呃_png.png", "呃呃", "【<sticker>ac_呃呃_png</sticker>】"),
        //        new("ac_吐血", "http://10.0.2.2:9000/sticker/ac/ac_吐血_png.png", "吐血", "【<sticker>ac_吐血_png</sticker>】"),
        //        new("ac_关爱", "http://10.0.2.2:9000/sticker/ac/ac_关爱_png.png", "关爱", "【<sticker>ac_关爱_png</sticker>】"),
        //        new("ac_不明咀栗", "http://10.0.2.2:9000/sticker/ac/ac_不明咀栗_png.png", "不明咀栗", "【<sticker>ac_不明咀栗_png</sticker>】"),
        //        new("ac_冷", "http://10.0.2.2:9000/sticker/ac/ac_冷_png.png", "冷", "【<sticker>ac_冷_png</sticker>】"),
        //        new("ac_你已经死了", "http://10.0.2.2:9000/sticker/ac/ac_你已经死了_png.png", "你已经死了", "【<sticker>ac_你已经死了_png</sticker>】"),
        //        new("ac_你啊", "http://10.0.2.2:9000/sticker/ac/ac_你啊_png.png", "你啊", "【<sticker>ac_你啊_png</sticker>】"),
        //        new("ac_吃糖", "http://10.0.2.2:9000/sticker/ac/ac_吃糖_png.png", "吃糖", "【<sticker>ac_吃糖_png</sticker>】"),
        //        new("ac_哭", "http://10.0.2.2:9000/sticker/ac/ac_哭_png.png", "哭", "【<sticker>ac_哭_png</sticker>】"),
        //        new("ac_哭泣", "http://10.0.2.2:9000/sticker/ac/ac_哭泣_png.png", "哭泣", "【<sticker>ac_哭泣_png</sticker>】"),
        //        new("ac_哼歌", "http://10.0.2.2:9000/sticker/ac/ac_哼歌_png.png", "哼歌", "【<sticker>ac_哼歌_png</sticker>】"),
        //        new("ac_好人的证明", "http://10.0.2.2:9000/sticker/ac/ac_好人的证明_png.png", "好人的证明", "【<sticker>ac_好人的证明_png</sticker>】"),
        //        new("ac_好痛", "http://10.0.2.2:9000/sticker/ac/ac_好痛_png.png", "好痛", "【<sticker>ac_好痛_png</sticker>】"),
        //        new("ac_低落", "http://10.0.2.2:9000/sticker/ac/ac_低落_png.png", "低落", "【<sticker>ac_低落_png</sticker>】"),
        //        new("ac_中二", "http://10.0.2.2:9000/sticker/ac/ac_中二_png.png", "中二", "【<sticker>ac_中二_png</sticker>】"),
        //        new("ac_C语言高手", "http://10.0.2.2:9000/sticker/ac/ac_C语言高手_png.png", "C语言高手", "【<sticker>ac_C语言高手_png</sticker>】"),
        //        new("ac_不是我", "http://10.0.2.2:9000/sticker/ac/ac_不是我_png.png", "不是我", "【<sticker>ac_不是我_png</sticker>】"),
        //        new("ac_一次性手套", "http://10.0.2.2:9000/sticker/ac/ac_一次性手套_png.png", "一次性手套", "【<sticker>ac_一次性手套_png</sticker>】"),
        //        new("ac_PrPr", "http://10.0.2.2:9000/sticker/ac/ac_PrPr_png.png", "PrPr", "【<sticker>ac_PrPr_png</sticker>】"),
        //        new("ac_受不了了", "http://10.0.2.2:9000/sticker/ac/ac_受不了了_png.png", "受不了了", "【<sticker>ac_受不了了_png</sticker>】"),
        //        new("ac_别过来", "http://10.0.2.2:9000/sticker/ac/ac_别过来_png.png", "别过来", "【<sticker>ac_别过来_png</sticker>】"),
        //        new("ac_凝视", "http://10.0.2.2:9000/sticker/ac/ac_凝视_png.png", "凝视", "【<sticker>ac_凝视_png</sticker>】"),
        //        new("ac_围观", "http://10.0.2.2:9000/sticker/ac/ac_围观_png.png", "围观", "【<sticker>ac_围观_png</sticker>】"),
        //        new("ac_壁咚", "http://10.0.2.2:9000/sticker/ac/ac_壁咚_png.png", "壁咚", "【<sticker>ac_壁咚_png</sticker>】"),
        //        new("ac_娇羞", "http://10.0.2.2:9000/sticker/ac/ac_娇羞_png.png", "娇羞", "【<sticker>ac_娇羞_png</sticker>】"),
        //        new("ac_没眼看", "http://10.0.2.2:9000/sticker/ac/ac_没眼看_png.png", "没眼看", "【<sticker>ac_没眼看_png</sticker>】"),
        //        new("ac_蔑视", "http://10.0.2.2:9000/sticker/ac/ac_蔑视_png.png", "蔑视", "【<sticker>ac_蔑视_png</sticker>】"),
        //        new("ac_药不能停", "http://10.0.2.2:9000/sticker/ac/ac_药不能停_png.png", "药不能停", "【<sticker>ac_药不能停_png</sticker>】"),
        //        new("ac_鬼脸", "http://10.0.2.2:9000/sticker/ac/ac_鬼脸_png.png", "鬼脸", "【<sticker>ac_鬼脸_png</sticker>】"),
        //        new("ac_推眼镜", "http://10.0.2.2:9000/sticker/ac/ac_推眼镜_png.png", "推眼镜", "【<sticker>ac_推眼镜_png</sticker>】"),
        //        new("ac_犀利", "http://10.0.2.2:9000/sticker/ac/ac_犀利_png.png", "犀利", "【<sticker>ac_犀利_png</sticker>】"),
        //        new("ac_愣", "http://10.0.2.2:9000/sticker/ac/ac_愣_png.png", "愣", "【<sticker>ac_愣_png</sticker>】"),
        //        new("ac_惊", "http://10.0.2.2:9000/sticker/ac/ac_惊_png.png", "惊", "【<sticker>ac_惊_png</sticker>】"),
        //        new("ac_悄悄话", "http://10.0.2.2:9000/sticker/ac/ac_悄悄话_png.png", "悄悄话", "【<sticker>ac_悄悄话_png</sticker>】"),
        //        new("ac_MDZZ", "http://10.0.2.2:9000/sticker/ac/ac_MDZZ_png.png", "MDZZ", "【<sticker>ac_MDZZ_png</sticker>】"),
        //        new("ac_不是吧", "http://10.0.2.2:9000/sticker/ac/ac_不是吧_png.png", "不是吧", "【<sticker>ac_不是吧_png</sticker>】"),
        //        new("ac_先跑路了", "http://10.0.2.2:9000/sticker/ac/ac_先跑路了_png.png", "先跑路了", "【<sticker>ac_先跑路了_png</sticker>】"),
        //        new("ac_参悟", "http://10.0.2.2:9000/sticker/ac/ac_参悟_png.png", "参悟", "【<sticker>ac_参悟_png</sticker>】"),
        //        new("ac_发现亮点", "http://10.0.2.2:9000/sticker/ac/ac_发现亮点_png.png", "发现亮点", "【<sticker>ac_发现亮点_png</sticker>】"),
        //        new("ac_只要微笑", "http://10.0.2.2:9000/sticker/ac/ac_只要微笑_png.png", "只要微笑", "【<sticker>ac_只要微笑_png</sticker>】"),
        //        new("ac_不怀好意", "http://10.0.2.2:9000/sticker/ac/ac_不怀好意_png.png", "不怀好意", "【<sticker>ac_不怀好意_png</sticker>】"),
        //        new("ac_上吊", "http://10.0.2.2:9000/sticker/ac/ac_上吊_png.png", "上吊", "【<sticker>ac_上吊_png</sticker>】"),
        //        new("ac_Lue", "http://10.0.2.2:9000/sticker/ac/ac_Lue_png.png", "Lue", "【<sticker>ac_Lue_png</sticker>】"),
        //        new("ac_niconico", "http://10.0.2.2:9000/sticker/ac/ac_niconico_png.png", "niconico", "【<sticker>ac_niconico_png</sticker>】"),
        //        new("ac_小鸟", "http://10.0.2.2:9000/sticker/ac/ac_小鸟_png.png", "小鸟", "【<sticker>ac_小鸟_png</sticker>】"),
        //    }));
        //}
    }
}
