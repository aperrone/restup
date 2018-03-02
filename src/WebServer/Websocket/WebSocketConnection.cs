using System;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace Restup.Webserver.WebSocket
{
    public class WebSocketConnection
    {
        private StreamSocket socket;

        internal void Init(StreamSocket socket)
        {
            this.socket = socket;
        }

        internal protected virtual void OnConnection(WebSocketRouteHandler handler)
        {
        }

        internal protected virtual void OnMessage(WebSocketRouteHandler handler, string msg)
        {
        }

        public async Task SendAsync(object json)
        {
            await SendAsync(Newtonsoft.Json.JsonConvert.SerializeObject(json));
        }

        public async Task SendAsync(string message)
        {
            try
            {
                using (var writer = new DataWriter(socket.OutputStream))
                {
                    await WebSocketRouteHandler.EncodeMessageAsync(writer, 129, message);
                    writer.DetachStream();
                }
            }
            catch (Exception e)
            {
            }
        }
    }
}
