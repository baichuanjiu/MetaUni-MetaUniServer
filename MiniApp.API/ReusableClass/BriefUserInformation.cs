namespace MiniApp.API.ReusableClass
{
    public class BriefUserInformation
    {
        public BriefUserInformation(int UUID, string avatar, string nickname, DateTime updatedTime)
        {
            this.UUID = UUID;
            Avatar = avatar;
            Nickname = nickname;
            UpdatedTime = updatedTime;
        }

        public int UUID { get; set; }
        public string Avatar { get; set; }
        public string Nickname { get; set; }
        public DateTime UpdatedTime { get; set; }
    }
}
