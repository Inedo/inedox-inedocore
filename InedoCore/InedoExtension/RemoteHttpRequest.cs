using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility.Agents;
using Inedo.IO;
using Inedo.Serialization;

namespace Inedo.Extensions
{
    [Serializable]
    internal sealed class RemoteHttpRequest
    {
        internal static readonly string DefaultUserAgent = $"{SDK.ProductName}/{SDK.ProductVersion.ToString(3)} (InedoCore/{typeof(RemoteHttpRequest).Assembly.GetName().Version.ToString()})";

        public RemoteHttpRequest(string url) : this("GET", url)
        {
        }
        public RemoteHttpRequest(string method, string url)
        {
            this.Method = method;
            this.Url = url;
        }

        public string Method { get; }
        public string Url { get; }
        public NameValueCollection Headers { get; } = new NameValueCollection();

        public string UserName { get; set; }
        public string Password { get; set; }

        public string UploadFromFile { get; set; }
        public byte[] UploadData { get; set; }
        public string DownloadToFile { get; set; }

        [field: NonSerialized]
        public Action<long, long?> Progress { get; set; }

        public async Task<RemoteHttpResponse> GetResponseAsync(Agent agent, CancellationToken cancellationToken)
        {
            var remoteJob = await agent.TryGetServiceAsync<IRemoteJobExecuter>();
            if (remoteJob != null)
                return await this.DoRemoteJob(agent, remoteJob, cancellationToken);

            var procExec = await agent.GetServiceAsync<IRemoteProcessExecuter>();
            if (await IsProgramAvailable(procExec, "wget", cancellationToken))
                return await this.DoWGet(agent, procExec, cancellationToken);
            if (await IsProgramAvailable(procExec, "curl", cancellationToken))
                return await this.DoCurl(agent, procExec, cancellationToken);

            throw new ExecutionFailureException($"On {agent.GetType().Name}, either wget or curl must be installed in order to perform remote HTTP requests.");
        }

        private static async Task<bool> IsProgramAvailable(IRemoteProcessExecuter procExec, string programName, CancellationToken cancellationToken)
        {
            using (var which = procExec.CreateProcess(new RemoteProcessStartInfo
            {
                FileName = "which",
                Arguments = procExec.EscapeArg(programName)
            }))
            {
                which.Start();
                await which.WaitAsync(cancellationToken);

                return which.ExitCode == 0;
            }
        }

        private async Task<RemoteHttpResponse> DoRemoteJob(Agent agent, IRemoteJobExecuter remoteJob, CancellationToken cancellationToken)
        {
            var job = new RemoteHttpJob(this);
            return (RemoteHttpResponse)await remoteJob.ExecuteJobAsync(job, cancellationToken);
        }
        private async Task<RemoteHttpResponse> DoCurl(Agent agent, IRemoteProcessExecuter procExec, CancellationToken cancellationToken)
        {
            var startInfo = new RemoteProcessStartInfo { FileName = "curl" };
            startInfo.AppendArgs(procExec, "-s", "-A", DefaultUserAgent, "-L", "-D", "-");
            startInfo.AppendArgs(procExec, "-X", this.Method, "--url", this.Url);
            startInfo.AppendArgs(procExec, "-o", AH.CoalesceString(this.DownloadToFile, "/dev/null"));
            if (!string.IsNullOrEmpty(this.UserName))
            {
                startInfo.AppendArgs(procExec, "--user", this.UserName + ":" + this.Password);
            }
            foreach (var name in this.Headers.AllKeys)
            {
                foreach (var value in this.Headers.GetValues(name))
                {
                    startInfo.AppendArgs(procExec, "-H", name + ": " + value);
                }
            }

            if (!string.IsNullOrEmpty(this.UploadFromFile))
            {
                startInfo.AppendArgs(procExec, "--data-binary", "@" + this.UploadFromFile);
            }
            else if (this.UploadData?.Length > 0)
            {
                startInfo.AppendArgs(procExec, "--data-raw", InedoLib.UTF8Encoding.GetString(this.UploadData));
            }

            using var process = procExec.CreateProcess(startInfo);
            var recorder = new RemoteHttpResponse.Recorder(this.Progress);
            process.OutputDataReceived += (s, e) =>
            {
                recorder.ProcessHeader(e.Data);
            };

            // TODO: find a way to get machine-readable progress information

            process.Start();
            await process.WaitAsync(cancellationToken);

            if (process.ExitCode != 0)
                throw new IOException("curl exited with code " + process.ExitCode);

            return recorder.Response;
        }
        private async Task<RemoteHttpResponse> DoWGet(Agent agent, IRemoteProcessExecuter procExec, CancellationToken cancellationToken)
        {
            var startInfo = new RemoteProcessStartInfo { FileName = "wget" };
            startInfo.AppendArgs(procExec, "-q", "--show-progress", "--progress", "dot");
            startInfo.AppendArgs(procExec, "-U", DefaultUserAgent, "-S", "--content-on-error");
            startInfo.AppendArgs(procExec, "--method", this.Method);
            startInfo.AppendArgs(procExec, "-O", AH.CoalesceString(this.DownloadToFile, "/dev/null"));
            if (!string.IsNullOrEmpty(this.UserName))
            {
                startInfo.AppendArgs(procExec, "--user", this.UserName, "--password", this.Password);
            }
            foreach (var name in this.Headers.AllKeys)
            {
                foreach (var value in this.Headers.GetValues(name))
                {
                    startInfo.AppendArgs(procExec, "--header", name + ": " + value);
                }
            }

            if (!string.IsNullOrEmpty(this.UploadFromFile))
            {
                startInfo.AppendArgs(procExec, "--body-file", this.UploadFromFile);
            }
            else if (this.UploadData?.Length > 0)
            {
                startInfo.AppendArgs(procExec, "--body-data", InedoLib.UTF8Encoding.GetString(this.UploadData));
            }

            startInfo.AppendArgs(procExec, "--", this.Url);

            using var process = procExec.CreateProcess(startInfo);
            var recorder = new RemoteHttpResponse.Recorder(this.Progress);
            process.ErrorDataReceived += (s, e) =>
            {
                if (!recorder.FinishedHeaders)
                {
                    recorder.ProcessHeader(e.Data.StartsWith("  ") ? e.Data.Substring(2) : e.Data);
                    return;
                }

                // [spaces] [numbers][letter] [some number of dots with spaces] [numbers][percent sign] [other stuff]
                // each dot represents 1 (binary) kilobyte downloaded; fifty per line max

                var dots = e.Data.SkipWhile(c => char.IsWhiteSpace(c))
                    .SkipWhile(c => char.IsDigit(c))
                    .SkipWhile(c => char.IsLetter(c))
                    .TakeWhile(c => !char.IsDigit(c))
                    .Count(c => c == '.');

                recorder.AdvanceProgress(1024 * dots);
            };

            process.Start();
            await process.WaitAsync(cancellationToken);

            if (process.ExitCode != 0)
                throw new IOException("wget exited with code " + process.ExitCode);

            return recorder.Response;
        }
    }

    [Serializable]
    internal sealed class RemoteHttpResponse
    {
        public int StatusCode { get; }
        public NameValueCollection Headers { get; }

        internal RemoteHttpResponse(int statusCode, NameValueCollection headers)
        {
            this.StatusCode = statusCode;
            this.Headers = headers;
        }

        internal sealed class Recorder
        {
            private readonly Action<long, long?> onProgress;
            private RemoteHttpResponse response;
            private long? contentLength;
            private long progress;
            public bool FinishedHeaders { get; private set; }

            public Recorder(Action<long, long?> progress)
            {
                this.onProgress = progress;
            }

            public RemoteHttpResponse Response
            {
                get
                {
                    if (this.response == null)
                        throw new IOException("Did not receive HTTP headers");

                    if (!this.FinishedHeaders)
                        throw new IOException("Did not finish receiving HTTP headers");

                    return this.response;
                }
            }

            public void ProcessHeader(string line)
            {
                if (this.FinishedHeaders)
                    throw new IOException($"Received line after headers already ended: {line}");

                if (this.response == null)
                {
                    var parts = line.Split(' ');
                    if (parts.Length >= 2 && parts[0].StartsWith("HTTP/") && parts[1].Length == 3)
                        response = new RemoteHttpResponse(Convert.ToInt32(parts[1], 10), new NameValueCollection());
                    else
                        throw new IOException($"Invalid first line of response headers: {line}");

                    return;
                }

                if (string.IsNullOrEmpty(line))
                {
                    if (this.response == null)
                        throw new IOException("Did not receive HTTP headers");

                    this.FinishedHeaders = true;
                    if (long.TryParse(response.Headers.Get("Content-Length"), out var len))
                        this.contentLength = len;
                }

                var colon = line.IndexOf(':');
                if (colon == -1)
                    throw new IOException($"Invalid header line: {line}");

                var name = line.Substring(0, colon);
                var value = line.Substring(colon + 1);
                if (value.StartsWith(" "))
                    value = value.Substring(1);

                this.response.Headers.Add(name, value);
            }

            public void AdvanceProgress(long amount)
            {
                if (this.response == null)
                    throw new IOException("Did not receive HTTP headers");

                this.progress += amount;
                this.onProgress?.Invoke(this.progress, this.contentLength);
            }
        }
    }

    internal sealed class RemoteHttpJob : RemoteJob
    {
        private RemoteHttpRequest request;

        public RemoteHttpJob(RemoteHttpRequest request)
        {
            this.request = request;
        }

        public override async Task<object> ExecuteAsync(CancellationToken cancellationToken)
        {
            var handler = new HttpClientHandler();
            if (!string.IsNullOrEmpty(this.request.UserName))
            {
                handler.Credentials = new NetworkCredential(this.request.UserName, this.request.Password);
            }

            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(RemoteHttpRequest.DefaultUserAgent);

            using var request = new HttpRequestMessage(new HttpMethod(this.request.Method), this.request.Url);
            foreach (var name in this.request.Headers.AllKeys)
            {
                request.Headers.Add(name, this.request.Headers.GetValues(name));
            }

            if (!string.IsNullOrEmpty(this.request.UploadFromFile))
            {
                request.Content = new StreamContent(new FileStream(this.request.UploadFromFile, FileMode.Open, FileAccess.Read));
            }
            else if (this.request.UploadData?.Length > 0)
            {
                request.Content = new ByteArrayContent(this.request.UploadData);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var len = response.Content.Headers.ContentLength;
            using var responseContent = await response.Content.ReadAsStreamAsync();

            using var output = string.IsNullOrEmpty(this.request.DownloadToFile) ? Stream.Null : new FileStream(this.request.DownloadToFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

            await responseContent.CopyToAsync(output, 4096, cancellationToken, pos => this.Post(SlimBinaryFormatter.SerializeToByteArray(new[] { pos, len })));

            var headers = new NameValueCollection();
            foreach (var kv in response.Headers)
            {
                foreach (var val in kv.Value)
                {
                    headers.Add(kv.Key, val);
                }
            }
            return new RemoteHttpResponse((int)response.StatusCode, headers);
        }

        protected override void DataReceived(byte[] data)
        {
            var progress = (long?[])SlimBinaryFormatter.DeserializeFromByteArray(data);
            this.request.Progress?.Invoke(progress[0].Value, progress[1]);
        }

        public override void Serialize(Stream stream)
        {
            SlimBinaryFormatter.Serialize(this.request, stream);
        }

        public override void Deserialize(Stream stream)
        {
            this.request = (RemoteHttpRequest)SlimBinaryFormatter.Deserialize(stream);
        }

        public override void SerializeResponse(Stream stream, object result)
        {
            SlimBinaryFormatter.Serialize(result, stream);
        }

        public override object DeserializeResponse(Stream stream)
        {
            return SlimBinaryFormatter.Deserialize(stream);
        }
    }
}
