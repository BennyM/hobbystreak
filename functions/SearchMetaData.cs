using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace HobbyStreak.Functions
{

    public class SearchMetaData
        : TableEntity

    {

        public string FileName { get; set; }
        public Int64 MaxId { get; set; }
        public Int64 SinceId { get; set; }
        public string RefreshUrl { get; set; }
        public string NextResults { get; set; }
        public int Count { get; set; }
        public double CompletedIn { get; set; }

        public string SinceIdStr { get; set; }
        public string Query { get; set; }

        public string MaxIdStr { get; set; }

    }
}