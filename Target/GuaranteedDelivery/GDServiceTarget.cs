using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using NLog.Config;
using NLog.Targets.NetworkJSON.ExtensionMethods;
using NLog.Targets.NetworkJSON.GuaranteedDelivery.LocalLogStorageDB;

namespace NLog.Targets.NetworkJSON.GuaranteedDelivery
{
    [Target("GDService")]
    public class GDServiceTarget : TargetWithLayout
    {
        #region Guaranteed Delivery Service Variables

        private Uri _endpoint;
        private SqliteConnection _dbConnection;
        private bool _disposed;

        #endregion

        public class GDServiceTypes
        {
            public const string socket = "socket";
            public const string elastic = "elastic";
        }

        #region Task Properties

        [Required]
        public string GuaranteedDeliveryDB { get; set; }

        [Required]
        public string Endpoint
        {
            get { return _endpoint.ToString(); }
            set
            {
                if (value != null)
                    _endpoint = new Uri(Environment.ExpandEnvironmentVariables(value));
                else
                    _endpoint = null;
            }
        }

        [Required]
        public string EndpointType { get; set; }

        public string EndpointExtraInfo { get; set; }

        private void VerifyDbConnection()
        {
            if (_dbConnection == null)
            {
                if (!File.Exists(GuaranteedDeliveryDB)) VerifyDbDirectory();
                _dbConnection = LogStorageConnection.OpenConnection(GuaranteedDeliveryDB);
                Debug.WriteLine($"Db Connection Created.");

                if (!LogStorageTable.TableExists(_dbConnection)) { LogStorageTable.CreateTable(_dbConnection); }
            }
        }

        private void VerifyDbDirectory()
        {
            var fileInfo = new FileInfo(GuaranteedDeliveryDB);
            try
            {
                var dbDirectory = fileInfo.Directory?.FullName;
                if (dbDirectory.IsNullOrEmpty()) throw new Exception();
                if (!Directory.Exists(dbDirectory)) Directory.CreateDirectory(dbDirectory);
            }
            catch
            {
                throw new Exception($"Unable to create or verify the directory structure for {GuaranteedDeliveryDB}");
            }
        }

        private void CloseDbConnection()
        {
            if (_dbConnection == null) return;
            Debug.WriteLine("DB Connection Closed.");
            _dbConnection.Close();
            _dbConnection.Dispose();
            _dbConnection = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                CloseDbConnection();
            }
            _disposed = true;
            base.Dispose(disposing);
        }

        [ArrayParameter(typeof(ParameterInfo), "parameter")]
        public IList<ParameterInfo> Parameters { get; }

        #endregion

        private IConverter Converter { get; }

        public GDServiceTarget() : this(new JsonConverter())
        {
        }

        public GDServiceTarget(IConverter converter)
        {
            Converter = converter;
            this.Parameters = new List<ParameterInfo>();
        }

        public void WriteLogEventInfo(LogEventInfo logEvent)
        {
            Write(logEvent);
        }

        protected override void Write(LogEventInfo logEvent)
        {
            foreach (var par in Parameters)
            {
                if (!logEvent.Properties.ContainsKey(par.Name))
                {
                    string stringValue = par.Layout.Render(logEvent);
                    logEvent.Properties.Add(par.Name, stringValue);
                }
            }

            var jsonObject = Converter.GetLogEventJson(logEvent);
            if (jsonObject == null) return;
            var jsonObjectStr = jsonObject.ToString(Formatting.None, null);
            Write(jsonObjectStr);
        }

        public void Write(string logEventAsJsonString)
        {
            try
            {
                VerifyDbConnection();
                LogStorageTable.TableExists(_dbConnection);
                LogStorageTable.InsertLogRecord(_dbConnection, Endpoint, EndpointType, EndpointExtraInfo, logEventAsJsonString);
            }
            catch (Exception)
            {
                CloseDbConnection();
                throw;
            }
        }

        public async Task WriteAsync(string logEventAsJsonString)
        {
            try
            {
                VerifyDbConnection();
                LogStorageTable.TableExists(_dbConnection);
                await LogStorageTable.InsertLogRecordAsync(_dbConnection, Endpoint, EndpointType, EndpointExtraInfo, logEventAsJsonString);
            }
            catch (Exception)
            {
                CloseDbConnection();
                throw;
            }
        }
    }
}