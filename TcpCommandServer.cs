using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimConnectBridge
{
    public class TcpCommandServer : IDisposable
    {
        private readonly int port;
        private readonly Action<string> lineHandler;
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private readonly ConcurrentDictionary<TcpClient, NetworkStream> clients = new ConcurrentDictionary<TcpClient, NetworkStream>();

        public TcpCommandServer(int port, Action<string> handler)
        {
            this.port = port;
            this.lineHandler = handler ?? throw new ArgumentNullException(nameof(handler));
            listener = new TcpListener(IPAddress.Loopback, port);
        }

        public void Start()
        {
            listener.Start();
            Task.Run(() => AcceptLoopAsync(cts.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    client.NoDelay = true;
                    var ns = client.GetStream();
                    if (clients.TryAdd(client, ns))
                    {
                        // start reader for this client
                        _ = Task.Run(() => ClientReadLoopAsync(client, ns, token));
                        Console.WriteLine($"TCP: Client connected from {client.Client.RemoteEndPoint}");
                    }
                    else
                    {
                        ns.Close();
                        client.Close();
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                Console.WriteLine("TcpCommandServer AcceptLoop error: " + ex.Message);
            }
        }

        private async Task ClientReadLoopAsync(TcpClient client, NetworkStream ns, CancellationToken token)
        {
            using (var sr = new StreamReader(ns, Encoding.ASCII))
            {
                try
                {
                    while (!token.IsCancellationRequested && client.Connected)
                    {
                        var line = await sr.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) break; // client closed
                        try
                        {
                            lineHandler(line);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("TcpCommandServer handler error: " + ex.Message);
                        }
                    }
                }
                catch (IOException) { }
                catch (Exception ex)
                {
                    Console.WriteLine("TcpCommandServer client read error: " + ex.Message);
                }
                finally
                {
                    // cleanup
                    clients.TryRemove(client, out var _);
                    try { ns.Close(); } catch { }
                    try { client.Close(); } catch { }
                    Console.WriteLine("TCP: Client disconnected");
                }
            }
        }

        public void Broadcast(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var bytes = Encoding.ASCII.GetBytes(text);
            foreach (var kv in clients)
            {
                var client = kv.Key;
                var ns = kv.Value;
                try
                {
                    if (client.Connected && ns.CanWrite)
                    {
                        ns.Write(bytes, 0, bytes.Length);
                        ns.Flush();
                    }
                }
                catch (Exception)
                {
                    // ignore per-client write errors; cleanup will happen on read loop
                }
            }
        }

        public void Stop()
        {
            cts.Cancel();
            try { listener.Stop(); } catch { }
            foreach (var kv in clients.Keys)
            {
                try { kv.Close(); } catch { }
            }
            clients.Clear();
        }

        public void Dispose()
        {
            Stop();
            cts.Dispose();
        }
    }
}
