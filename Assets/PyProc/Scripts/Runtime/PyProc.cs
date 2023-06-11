// -----------------------------------------------------------------------
// <copyright file="PyProc.cs" company="AillieoTech">
// Copyright (c) AillieoTech. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace AillieoUtils
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The PyProc class is a C# class for starting a Python script and communicating with it.
    /// </summary>
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

        /// <summary>
        /// Initializes a new instance of the <see cref="PyProc"/> class using the default Python interpreter to start the specified Python script.
        /// </summary>
        /// <param name="pythonScriptPath">The path to the Python script to start.</param>
        public PyProc(string pythonScriptPath)
            : this("python", pythonScriptPath)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PyProc"/> class using the specified Python interpreter to start the specified Python script.
        /// </summary>
        /// <param name="pythonExecutable">The path to the Python interpreter to use.</param>
        /// <param name="pythonScriptPath">The path to the Python script to start.</param>
        public PyProc(string pythonExecutable, string pythonScriptPath)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.dataQueue = new ConcurrentQueue<string>();

            this.listener = new TcpListener(IPAddress.Any, 0);
            this.listener.Start();
            var port = ((IPEndPoint)this.listener.LocalEndpoint).Port;

            var keyBytes = new byte[128];
            using (RandomNumberGenerator rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(keyBytes);
            }

            var key = Convert.ToBase64String(keyBytes);

            this.StartAcceptingConnectionsAsync(keyBytes).Await();
            var startInfo = new ProcessStartInfo(pythonExecutable, $"{pythonScriptPath} {port} {key}")
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

        /// <summary>
        /// Finalizes an instance of the <see cref="PyProc"/> class.
        /// </summary>
        ~PyProc()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Event that is invoked when data is received from the Python script.
        /// </summary>
        public event Action<string> OnData;

        /// <summary>
        /// Event that is invoked when data is received from the standard output stream of the Python script.
        /// </summary>
        public event Action<string> OnOutput;

        /// <summary>
        /// Event that is invoked when data is received from the standard error stream of the Python script.
        /// </summary>
        public event Action<string> OnError;

        /// <summary>
        /// Sends data to the Python script.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
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

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private async Task StartAcceptingConnectionsAsync(byte[] keyBytes)
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var rawClient = await this.listener.AcceptTcpClientAsync();
                    var valid = await this.Handshake(rawClient, keyBytes);
                    if (!valid)
                    {
                        continue;
                    }

                    if (this.client != null)
                    {
                        break;
                    }

                    this.client = rawClient;
                    this.stream = this.client.GetStream();
                    this.reader = new StreamReader(this.stream);
                    this.writer = new StreamWriter(this.stream) { AutoFlush = true };

                    // Send any queued data to the new client
                    while (this.dataQueue.TryDequeue(out var data))
                    {
                        this.writer.WriteLineAsync(data).Await();
                    }

                    this.StartReadingDataAsync().Await();

                    break;
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

        private async Task<bool> Handshake(TcpClient rawClient, byte[] key)
        {
            var stream = rawClient.GetStream();

            var keyBuffer = new byte[key.Length];
            await stream.ReadAsync(keyBuffer, 0, keyBuffer.Length);

            for (var i = 0; i < key.Length; ++i)
            {
                if (key[i] != keyBuffer[i])
                {
                    rawClient.Close();
                    return false;
                }
            }

            return true;
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
