using Restup.HttpMessage;
using Restup.HttpMessage.Models.Schemas;
using Restup.Webserver.Models.Contracts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Restup.Webserver.WebSocket
{
    public abstract class WebSocketRouteHandler
    {
        protected List<WebSocketConnection> Connections = new List<WebSocketConnection>();

        public async Task SendAsync(object json, WebSocketConnection exclude = null)
        {
            await SendAsync(Newtonsoft.Json.JsonConvert.SerializeObject(json), exclude);
        }

        public async Task SendAsync(string message, WebSocketConnection exclude = null)
        {
            var targets = Connections
                .ToArray(); // to preserve from multitreading
            foreach (var t in targets)
            {
                if (t != exclude)
                    await t.SendAsync(message);
            }
        }

        internal static async Task EncodeMessageAsync(DataWriter writer, byte code, string message)
        {
            byte[] content = Encoding.UTF8.GetBytes(message);
            byte[] head;

            Int32 length = content.Length;

            if (length <= 125)
            {
                head = new byte[2];
                head[1] = (byte)length;
            }
            else if (length >= 126 && length <= 65535)
            {
                head = new byte[4];
                head[1] = (Byte)126;
                head[2] = (Byte)((length >> 8) & 255);
                head[3] = (Byte)(length & 255);
            }
            else
            {
                head = new byte[10];
                head[1] = (Byte)127;
                head[2] = (Byte)((length >> 56) & 255);
                head[3] = (Byte)((length >> 48) & 255);
                head[4] = (Byte)((length >> 40) & 255);
                head[5] = (Byte)((length >> 32) & 255);
                head[6] = (Byte)((length >> 24) & 255);
                head[7] = (Byte)((length >> 16) & 255);
                head[8] = (Byte)((length >> 8) & 255);
                head[9] = (Byte)(length & 255);
            }

            head[0] = (byte)code;

            byte[] response = new Byte[head.Length + length];

            //Add the frame bytes to the reponse
            head.CopyTo(response, 0);

            //Add the data bytes to the response
            content.CopyTo(response, head.Length);

            writer.WriteBytes(response);
            await writer.StoreAsync();
        }

        internal static async Task<string> DecodeStreamAsync(DataReader reader)
        {
            byte b0 = await reader.LoadByteAsync();

            if (b0 != 129 && b0 != 136)
                throw new NotSupportedException("Unsupported protocol code " + b0);

            Debug.WriteLine("b0" + b0);
            byte b1 = await reader.LoadByteAsync();
            int dataLength = 0;

            if (b1 - 128 <= 125)
            {
                dataLength = b1 - 128;
            }

            if (b1 - 128 == 126)
            {
                byte b2 = await reader.LoadByteAsync();
                byte b3 = await reader.LoadByteAsync();
                dataLength = BitConverter.ToInt16(new byte[] { b3, b2 }, 0);
            }

            if (b1 - 128 == 127)
            {
                byte b2 = await reader.LoadByteAsync();
                byte b3 = await reader.LoadByteAsync();
                byte b4 = await reader.LoadByteAsync();
                byte b5 = await reader.LoadByteAsync();
                byte b6 = await reader.LoadByteAsync();
                byte b7 = await reader.LoadByteAsync();
                byte b8 = await reader.LoadByteAsync();
                byte b9 = await reader.LoadByteAsync();
                dataLength = (int)BitConverter.ToInt64(new byte[] { b9, b8, b7, b6, b5, b4, b3, b2 }, 0);
            }

            byte[] key = new byte[4];
            await reader.LoadBlockAsync(key);

            byte[] data = new byte[dataLength];
            await reader.LoadBlockAsync(data);

            for (int i = 0; i < data.Length; i++)
                data[i] = (byte)(data[i] ^ key[i % 4]);

            if (b0 == 136)
                return null; // close request;

            return Encoding.ASCII.GetString(data, 0, dataLength);
        }
    }

    public class WebSocketRouteHandler<T> : WebSocketRouteHandler, IRouteHandler, IStreamHandler where T : WebSocketConnection, new()
    {
        async Task<HttpServerResponse> IRouteHandler.HandleRequest(IHttpServerRequest request)
        {
            string protocol = request.Headers.FirstOrDefault(h => h.Name == "Sec-WebSocket-Protocol")?.Value;
            string secWebSocketKey = request.Headers.FirstOrDefault(h => h.Name == "Sec-WebSocket-Key")?.Value;
            byte[] secWebSocketAccept = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(secWebSocketKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));

            HttpServerResponse httpResponse = HttpServerResponse.Create(HttpResponseStatus.SwitchingProtocols);
            httpResponse.AddHeader("Connection", "Upgrade");
            httpResponse.AddHeader("Upgrade", "websocket");
            httpResponse.AddHeader("Sec-WebSocket-Accept", Convert.ToBase64String(secWebSocketAccept));
            if (protocol != null)
                httpResponse.AddHeader("Sec-WebSocket-Protocol", protocol);

            return httpResponse;
        }

        async Task IStreamHandler.Stream(StreamSocket socket)
        {
            Debug.WriteLine("Stream starting...");

            var conn = new T();
            conn.Init(socket);

            try
            {
                conn.OnConnection(this);

                Connections.Add(conn);

                using (var reader = new DataReader(socket.InputStream))
                {
                    while (true)
                    {
                        string msg = await DecodeStreamAsync(reader);

                        if (msg == null)
                            break;

                        Debug.WriteLine(msg);
                        conn.OnMessage(this, msg);
                    }

                    using (var writer = new DataWriter(socket.OutputStream))
                    {
                        await EncodeMessageAsync(writer, 136, "");
                        writer.DetachStream();
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
                Debug.WriteLine("Stream exiting...");

                Connections.Remove(conn);
            }
        }
    }

    static class Ext
    {
        public static async Task<byte> LoadByteAsync(this DataReader reader)
        {
            await reader.LoadAsync(1);
            return reader.ReadByte();
        }

        public static async Task LoadBlockAsync(this DataReader reader, byte[] buffer)
        {
            if (buffer.Length == 0)
                return;

            await reader.LoadAsync((uint)buffer.Length);
            reader.ReadBytes(buffer);
        }
    }
}
