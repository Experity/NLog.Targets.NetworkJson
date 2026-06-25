using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NLog.Targets.NetworkJSON.ExtensionMethods;

namespace NLog.Targets.NetworkJSON.GuaranteedDelivery.LocalLogStorageDB
{
    public class LogStorageTable
    {
        public const string TableName = "LogStorage";

        public class Columns
        {
            public static ColumnInfo MessageId { get; } = new ColumnInfo(nameof(MessageId), "INTEGER PRIMARY KEY ASC", DbType.Int64, 0);
            public static ColumnInfo Endpoint { get; } = new ColumnInfo(nameof(Endpoint), "NVARCHAR(2048)", DbType.String, 1);
            public static ColumnInfo EndpointType { get; } = new ColumnInfo(nameof(EndpointType), "NVARCHAR(20)", DbType.String, 2);
            public static ColumnInfo EndpointExtraInfo { get; } = new ColumnInfo(nameof(EndpointExtraInfo), "NVARCHAR(512)", DbType.String, 3);
            public static ColumnInfo LogMessage { get; } = new ColumnInfo(nameof(LogMessage), "TEXT", DbType.String, 4);
            public static ColumnInfo CreatedOn { get; } = new ColumnInfo(nameof(CreatedOn), "DATETIME", DbType.DateTime, 5);
            public static ColumnInfo RetryCount { get; } = new ColumnInfo(nameof(RetryCount), "INT2", DbType.Int16, 6);
        }

        public static bool TableExists(SqliteConnection dbConnection)
        {
            var tableExistsSql = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{TableName}'";
            var cmd = new SqliteCommand(tableExistsSql, dbConnection);
            var tableName = cmd.ExecuteScalar()?.ToString();
            return (!tableName.IsNullOrEmpty());
        }

        public static void CreateTable(SqliteConnection dbConnection)
        {
            var tableCreateSql = $"CREATE TABLE {TableName} ({Columns.MessageId.ColumnName} {Columns.MessageId.ColumnDDL}, {Columns.Endpoint.ColumnName} {Columns.Endpoint.ColumnDDL}, {Columns.EndpointType.ColumnName} {Columns.EndpointType.ColumnDDL}, {Columns.EndpointExtraInfo.ColumnName} {Columns.EndpointExtraInfo.ColumnDDL}, {Columns.LogMessage.ColumnName} {Columns.LogMessage.ColumnDDL}, {Columns.CreatedOn.ColumnName} {Columns.CreatedOn.ColumnDDL}, {Columns.RetryCount.ColumnName} {Columns.RetryCount.ColumnDDL})";
            var cmd = new SqliteCommand(tableCreateSql, dbConnection);
            cmd.ExecuteNonQuery();
        }

        public static int InsertLogRecord(SqliteConnection dbConnection, string endpoint, string endpointType, string endpointExtraInfo, string logMessage)
        {
            var cmd = BuildInsertCommand(dbConnection, endpoint, endpointType, endpointExtraInfo, logMessage);
            return cmd.ExecuteNonQuery();
        }

        public static async Task<int> InsertLogRecordAsync(SqliteConnection dbConnection, string endpoint, string endpointType, string endpointExtraInfo, string logMessage)
        {
            var cmd = BuildInsertCommand(dbConnection, endpoint, endpointType, endpointExtraInfo, logMessage);
            return await cmd.ExecuteNonQueryAsync();
        }

        private static SqliteCommand BuildInsertCommand(SqliteConnection dbConnection, string endpoint, string endpointType, string endpointExtraInfo, string logMessage)
        {
            var dataInsertSql = $"INSERT INTO {TableName} ({Columns.Endpoint.ColumnName}, {Columns.EndpointType.ColumnName}, {Columns.EndpointExtraInfo.ColumnName}, {Columns.LogMessage.ColumnName}, {Columns.RetryCount.ColumnName}, {Columns.CreatedOn.ColumnName}) VALUES ({Columns.Endpoint.ParameterName}, {Columns.EndpointType.ParameterName}, {Columns.EndpointExtraInfo.ParameterName}, {Columns.LogMessage.ParameterName}, {Columns.RetryCount.ParameterName}, {Columns.CreatedOn.ParameterName})";
            var cmd = new SqliteCommand(dataInsertSql, dbConnection);

            var param = Columns.Endpoint.GetParamterForColumn();
            param.Value = endpoint;
            cmd.Parameters.Add(param);

            param = Columns.EndpointType.GetParamterForColumn();
            param.Value = endpointType;
            cmd.Parameters.Add(param);

            param = Columns.EndpointExtraInfo.GetParamterForColumn();
            param.Value = (object)endpointExtraInfo ?? DBNull.Value;
            cmd.Parameters.Add(param);

            param = Columns.LogMessage.GetParamterForColumn();
            param.Value = logMessage;
            cmd.Parameters.Add(param);

            param = Columns.RetryCount.GetParamterForColumn();
            param.Value = 0;
            cmd.Parameters.Add(param);

            param = Columns.CreatedOn.GetParamterForColumn();
            param.Value = DateTime.Now.ToString("o");
            cmd.Parameters.Add(param);

            return cmd;
        }

        public static DataTable GetFirstTryRecords(SqliteConnection dbConnection, int selectCount)
        {
            var dataSelectSql = $"SELECT * FROM {TableName} WHERE {Columns.RetryCount.ColumnName} = 0 LIMIT {selectCount}";
            var cmd = new SqliteCommand(dataSelectSql, dbConnection);
            var dt = new DataTable(TableName);
            using (var reader = cmd.ExecuteReader())
            {
                dt.Load(reader);
            }
            return dt;
        }

        public static DataTable GetRetryRecords(SqliteConnection dbConnection, int selectCount)
        {
            var dataSelectSql = $"SELECT * FROM {TableName} WHERE {Columns.RetryCount.ColumnName} > 0 ORDER BY RetryCount ASC, MessageId ASC LIMIT {selectCount}";
            var cmd = new SqliteCommand(dataSelectSql, dbConnection);
            var dt = new DataTable(TableName);
            using (var reader = cmd.ExecuteReader())
            {
                dt.Load(reader);
            }
            return dt;
        }

        public static DataTable GetFailedRecords(SqliteConnection dbConnection, int selectCount, int expiredMinutes)
        {
            var dataSelectSql = $"SELECT * FROM {TableName} WHERE {Columns.RetryCount.ColumnName} > 2 AND Cast((JulianDay() - JulianDay({Columns.CreatedOn.ColumnName})) * 24 * 60 As Integer) > {expiredMinutes} LIMIT {selectCount}";
            var cmd = new SqliteCommand(dataSelectSql, dbConnection);
            var dt = new DataTable(TableName);
            using (var reader = cmd.ExecuteReader())
            {
                dt.Load(reader);
            }
            return dt;
        }

        public static int UpdateLogRecordRetryCount(SqliteConnection dbConnection, long messageId)
        {
            var dataUpdateSql = $"UPDATE {TableName} SET {Columns.RetryCount.ColumnName} = {Columns.RetryCount.ColumnName} + 1 WHERE {Columns.MessageId.ColumnName} = {messageId}";
            Debug.WriteLine(dataUpdateSql);
            var cmd = new SqliteCommand(dataUpdateSql, dbConnection);
            return cmd.ExecuteNonQuery();
        }

        public static int UpdateLogRecordsRetryCount(SqliteConnection dbConnection, long[] messageIds)
        {
            var dataUpdateSql = $"UPDATE {TableName} SET {Columns.RetryCount.ColumnName} = {Columns.RetryCount.ColumnName} + 1 WHERE {Columns.MessageId.ColumnName} in ({string.Join(",", messageIds)})";
            Debug.WriteLine(dataUpdateSql);
            var cmd = new SqliteCommand(dataUpdateSql, dbConnection);
            return cmd.ExecuteNonQuery();
        }

        public static int DeleteProcessedRecord(SqliteConnection dbConnection, long messageId)
        {
            var dataDeleteSql = $"DELETE FROM {TableName} WHERE {Columns.MessageId.ColumnName} = {messageId}";
            var cmd = new SqliteCommand(dataDeleteSql, dbConnection);
            return cmd.ExecuteNonQuery();
        }

        public static int DeleteProcessedRecords(SqliteConnection dbConnection, long[] messageIds)
        {
            var dataDeleteSql = $"DELETE FROM {TableName} WHERE {Columns.MessageId.ColumnName} in ({string.Join(",", messageIds)})";
            var cmd = new SqliteCommand(dataDeleteSql, dbConnection);
            return cmd.ExecuteNonQuery();
        }

        public static long GetBacklogCount(SqliteConnection dbConnection)
        {
            var dataSelectSql = $"SELECT COUNT(*) FROM {TableName}";
            var cmd = new SqliteCommand(dataSelectSql, dbConnection);
            return (long)cmd.ExecuteScalar();
        }
    }
}