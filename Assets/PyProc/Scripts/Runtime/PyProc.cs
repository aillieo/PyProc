namespace AillieoUtils
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    public class PyProc : IDisposable
    {
        private readonly Process process;
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ConcurrentQueue<string> dataQueue;

        private TcpClient client;
        private NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer;

        private bool disposed;

        public PyProc(string pythonScriptPath)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.dataQueue = new ConcurrentQueue<string>();

            this.listener = new TcpListener(IPAddress.Any, 0);
            this.listener.Start();
            var port = ((IPEndPoint)this.listener.LocalEndpoint).Port;

            this.StartAcceptingConnectionsAsync().Await();

            var startInfo = new ProcessStartInfo("python", $"{pythonScriptPath} {port}")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            this.process = new Process { StartInfo = startInfo };

            this.process.Start();

            this.process.OutputDataReceived += (s, e) => this.OnOutput?.Invoke(e.Data);
            this.process.BeginOutputReadLine();

            this.process.ErrorDataReceived += (s, e) => this.OnError?.Invoke(e.Data);
            this.process.BeginErrorReadLine();
        }

        ~PyProc()
        {
            this.Dispose(false);
        }

        public event Action<string> OnData;

        public event Action<string> OnOutput;

        public event Action<string> OnError;

        public async Task SendAsync(string data)
        {
            if (this.writer != null)
            {
                await this.writer.WriteLineAsync(data).ConfigureAwait(false);
            }
            else
            {
                this.dataQueue.Enqueue(data);
            }
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private async Task StartAcceptingConnectionsAsync()
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    this.client = await this.listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    this.stream = this.client.GetStream();
                    this.reader = new StreamReader(this.stream);
                    this.writer = new StreamWriter(this.stream) { AutoFlush = true };

                    // Send any queued data to the new client
                    while (this.dataQueue.TryDequeue(out var data))
                    {
                        await this.writer.WriteLineAsync(data).ConfigureAwait(false);
                    }

                    this.StartReadingDataAsync().Await();
                }
                catch (ObjectDisposedException e) when (e.ObjectName == typeof(Socket).FullName)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException)
                {
                    break;
                }
            }

            this.listener.Stop();
        }

        private async Task StartReadingDataAsync()
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var data = await this.reader.ReadLineAsync().ConfigureAwait(false);
                    if (data == null)
                    {
                        break;
                    }
                    else
                    {
                        this.OnData?.Invoke(data);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }
            }

            this.writer?.Close();
            this.reader?.Close();
            this.stream?.Close();
            this.client?.Close();
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                }

                this.cancellationTokenSource.Cancel();
                this.listener?.Stop();
                this.process?.Kill();
                this.process?.Dispose();
                this.writer?.Dispose();
                this.reader?.Dispose();
                this.stream?.Dispose();
                this.client?.Dispose();

                this.disposed = true;
            }
        }
    }
}
