using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Fleck;
using log4net;
using log4net.Appender;
using log4net.Core;

namespace Zenviro.Ninja
{
    public class Fleck
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Fleck));
        #region singleton

        static readonly object Lock = new object();
        static Fleck _instance;
        public static Fleck Instance
        {
            get
            {
                lock (Lock)
                    return _instance ?? (_instance = new Fleck());
            }
        }

        #endregion

        static readonly string Uri = string.Format("ws://localhost:{0}", ConfigurationManager.AppSettings.Get("FleckPort"));

        private Fleck()
        {
            _sockets = new List<IWebSocketConnection>();
            _server = new WebSocketServer(Uri);
            _server.Start(socket =>
            {
                socket.OnOpen = () => _sockets.Add(socket);
                socket.OnClose = () => _sockets.Remove(socket);
                socket.OnMessage = Broadcast;
            });
        }

        public void Init()
        {
            Log.Info(string.Format("fleck server initialised at: {0}", Uri));
        }

        public void Run()
        {
            Log.Info(string.Format("fleck server running at: {0}", Uri));
        }

        public void Broadcast(string message)
        {
            _sockets.ToList().ForEach(s => s.Send(message));
        }

        public void Stop()
        {
            Log.Info(string.Format("fleck server at: {0}, stopping", Uri));
            _server.Dispose();
        }

        private readonly List<IWebSocketConnection> _sockets;
        private readonly WebSocketServer _server;
    }

    public class FleckAppender : AppenderSkeleton
    {
        protected override void Append(LoggingEvent loggingEvent)
        {
            Fleck.Instance.Broadcast(RenderLoggingEvent(loggingEvent));
        }
    }
}
