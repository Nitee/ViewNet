using System;
using ViewNet;
using System.Net;
using System.Threading;

namespace TestConsole
{
	class MainClass
	{
		[STAThread]
		public static void Main ()
		{
			Thread.CurrentThread.Name = "Main Thread";
			TestServiceManager ();
		}

		public static void TestServiceManager ()
		{
			var selector = new IPEndPoint (IPAddress.Loopback, 3333);
			var server = new DomainManager ();
			server.StartHosting (selector.Address, selector.Port);
			var client = new DomainManager ();

			client.ConnectToHost (selector.Address, selector.Port);
			var checkSystem = new FullSystemMonitor ();
			client.AddServiceToNode (selector, checkSystem);

			var timer = new System.Timers.Timer ();
			timer.AutoReset = true;
			timer.Enabled = true;
			timer.Interval = 1000;
			timer.Elapsed += (sender, e) => {
				checkSystem.SendCPUInformation();
				checkSystem.PingToRemoteComputer();

				Console.WriteLine("Ping: {0}", checkSystem.RemoteComputerPing.PingRate);
				Console.WriteLine("CPU: {0}", checkSystem.RemoteComputerCPU.CpuTick);
			};
			timer.Start ();
			bool isActive = true;
			while (isActive) {
				Console.CancelKeyPress += (sender, e) => isActive = false;
				Thread.Sleep (0);
			}
			timer.Stop ();
		}
	}
}
