using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Table;

namespace HobbyStreak.Functions
{
    public class Ops
    {
        public static async Task InsertOrMerge<T>(CloudTable tbl, T setting)
          where T : TableEntity
        {
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(setting);
            await tbl.ExecuteAsync(insertOrMergeOperation);
        }
    }
}