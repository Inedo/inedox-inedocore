using System;
using System.IO;
using Inedo.Agents;
using Inedo.Diagnostics;

namespace Inedo.Extensions.Operations.ProGet.AssetDirectories
{
    internal abstract class AssetDirectoryJob : RemoteJob, IRemotableAssetOperation
    {
        private int? percent;
        private string message;

        protected AssetDirectoryJob()
        {
        }
        protected AssetDirectoryJob(AssetDirectoryOperation operation)
        {
            this.Path = operation.Path;
            this.ApiUrl = operation.ApiUrl;
            this.ApiKey = operation.ApiKey;
            this.UserName = operation.UserName;
            this.Password = operation.Password;
        }

        public string Path { get; set; }
        public string ApiUrl { get; set; }
        public string ApiKey { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public Action<int?, string> ProgressReceived { get; set; }

        public override void Serialize(Stream stream)
        {
            using var writer = new BinaryWriter(stream, InedoLib.UTF8Encoding, true);
            writer.Write(this.Path ?? string.Empty);
            writer.Write(this.ApiUrl ?? string.Empty);
            writer.Write(this.ApiKey ?? string.Empty);
            writer.Write(this.UserName ?? string.Empty);
            writer.Write(this.Password ?? string.Empty);
        }
        public override void Deserialize(Stream stream)
        {
            using var reader = new BinaryReader(stream, InedoLib.UTF8Encoding, true);
            this.Path = reader.ReadString();
            this.ApiUrl = reader.ReadString();
            this.ApiKey = reader.ReadString();
            this.UserName = reader.ReadString();
            this.Password = reader.ReadString();
        }
        public override object DeserializeResponse(Stream stream) => null;
        public override void SerializeResponse(Stream stream, object result)
        {
        }

        protected override void DataReceived(byte[] data)
        {
            var report = this.ProgressReceived;
            if (report != null && data.Length > 0)
            {
                int? percent = data[0] <= 100 ? data[0] : null;
                var message = data.Length > 1 ? InedoLib.UTF8Encoding.GetString(data, 1, data.Length - 1) : null;
                report(percent, message);
            }
        }
        protected void ReportProgress(int? percent, string message)
        {
            if (this.percent != percent || this.message != message)
            {
                this.percent = percent;
                this.message = message;

                byte[] buffer;
                if (message == null)
                {
                    buffer = new byte[1];
                    buffer[0] = percent >= 0 && percent <= 100 ? (byte)percent : (byte)0xFF;
                }
                else
                {
                    buffer = new byte[InedoLib.UTF8Encoding.GetByteCount(message) + 1];
                    buffer[0] = percent >= 0 && percent <= 100 ? (byte)percent : (byte)0xFF;
                    InedoLib.UTF8Encoding.GetBytes(message, 0, message.Length, buffer, 1);
                }

                this.Post(buffer);
            }
        }

        void ILogSink.Log(IMessage message) => this.Log(message.Level, message.Message);
    }
}
