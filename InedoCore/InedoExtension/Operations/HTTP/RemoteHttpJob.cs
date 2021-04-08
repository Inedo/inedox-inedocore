using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Serialization;

namespace Inedo.Extensions.Operations.HTTP
{
    internal sealed class RemoteHttpJob : RemoteJob
    {
        public HttpOperationBase Operation { get; set; }

        public override void Serialize(Stream stream)
        {
            SlimBinaryFormatter.Serialize(this.Operation, stream);
        }

        public override void Deserialize(Stream stream)
        {
            this.Operation = (HttpOperationBase)SlimBinaryFormatter.Deserialize(stream);
        }

        public override void SerializeResponse(Stream stream, object result)
        {
            SlimBinaryFormatter.Serialize(result, stream);
        }

        public override object DeserializeResponse(Stream stream)
        {
            return SlimBinaryFormatter.Deserialize(stream);
        }

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            this.Operation.MessageLogged += (s, e) => this.Log(e.Level, e.Message);
            await this.Operation.PerformRequestAsync(cancellationToken).ConfigureAwait(false);
            return this.Operation.ResponseBodyVariable;
        }
    }
}
