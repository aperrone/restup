using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace Restup.Webserver.Models.Contracts
{
    public interface IStreamHandler
    {
        Task Stream(StreamSocket socket);
    }
}