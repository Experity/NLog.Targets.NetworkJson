using Microsoft.Data.Sqlite;

namespace NLog.Targets.NetworkJSON.GuaranteedDelivery.LocalLogStorageDB
{
    public class LogStorageConnection
    {
        public static SqliteConnection OpenConnection(string dbFileName)
        {
            var dbConnection = new SqliteConnection($"Data Source={dbFileName};Pooling=True;");
            dbConnection.Open();
            return dbConnection;
        }
    }
}