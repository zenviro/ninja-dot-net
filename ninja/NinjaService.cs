using System.ServiceProcess;
using System.Threading.Tasks;

namespace Zenviro.Ninja
{
    public partial class NinjaService : ServiceBase
    {
        public NinjaService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Fleck.Instance.Init();
            Task.Factory.StartNew(() => Fleck.Instance.Run());
            AppConfig.InitDataDir();
            Monitor.Instance.Init();
            Task.Factory.StartNew(() => Monitor.Instance.Run());
        }

        protected override void OnStop()
        {
            Monitor.Instance.Stop();
            Fleck.Instance.Stop();
        }
    }
}
