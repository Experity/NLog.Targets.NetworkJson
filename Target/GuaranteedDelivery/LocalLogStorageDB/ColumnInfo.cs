using System.Data;
using Microsoft.Data.Sqlite;

namespace NLog.Targets.NetworkJSON.GuaranteedDelivery.LocalLogStorageDB
{
    public class ColumnInfo
    {
        public ColumnInfo(string columnName, string columnDDL, DbType parameterType, int columnIndex)
        {
            ColumnName = columnName;
            ColumnDDL = columnDDL;
            Index = columnIndex;
            ParameterName = $"@{columnName}";
            ParameterType = parameterType;
        }

        public string ColumnName { get; }
        public string ColumnDDL { get; }
        public int Index { get; }
        public string ParameterName { get; }
        public DbType ParameterType { get; }

        public SqliteParameter GetParamterForColumn()
        {
            return new SqliteParameter(ParameterName, ParameterType);
        }
    }
}