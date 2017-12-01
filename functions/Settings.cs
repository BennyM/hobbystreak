using Microsoft.WindowsAzure.Storage.Table;

namespace HobbyStreak.Functions
{
    public class Settings
        : TableEntity
    {
        public Settings()
        {

        }


        public string Value { get; set; }

    }
}
