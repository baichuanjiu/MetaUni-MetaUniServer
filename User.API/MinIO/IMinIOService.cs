namespace User.API.MinIO
{
    public interface IMinIOService
    {
        //入参：图片名、图片流
        //返回值：存储是否成功
        Task<bool> UploadImageAsync(string imageName, Stream file);
    }
}
