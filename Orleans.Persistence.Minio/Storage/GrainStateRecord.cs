namespace Orleans.Persistence.Minio.Storage
{
    public partial class MinioGrainStorage
    {
        public class GrainStateRecord
        {
            public int ETag { get; set; }
            public object State { get; set; }
        }
    }
}
