using System;
using System.Diagnostics;

namespace ViewNet
{
	public class FullSystemMonitor : IService
	{
		ByteServiceStream InternalServiceStream = new ByteServiceStream(new [] {typeof(CPUFeed), typeof(PingFeed)});

		public CPUFeed RemoteComputerCPU { get; set;}
		public PingFeed RemoteComputerPing { get; set;}
		PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

		public FullSystemMonitor ()
		{
			RemoteComputerCPU = new CPUFeed ();
			RemoteComputerPing = new PingFeed ();
		}

		public void PingToRemoteComputer()
		{
			var newFeed = new PingFeed ();
			InternalServiceStream.EnqueueMessage (newFeed);
		}

		public void SendCPUInformation()
		{
			var cpuFeed = new CPUFeed (cpuCounter.NextValue());
			InternalServiceStream.EnqueueMessage (cpuFeed);
		}

		#region IService implementation

		public byte[] Write ()
		{
			return InternalServiceStream.Write (65536);
		}

		public void Read (byte[] data)
		{
			InternalServiceStream.Read (data);

			while (true) {
				var dequeued = InternalServiceStream.AttemptDequeueMessage ();
				if (dequeued == null)
					break;

				if (dequeued.GetType () == typeof(CPUFeed)) {
					RemoteComputerCPU = (CPUFeed)dequeued;
					if (RemoteComputerCPU.CpuTick < Single.Epsilon) {
						cpuCounter.NextValue ();
						var newFeed = new CPUFeed (cpuCounter.NextValue ());
						InternalServiceStream.EnqueueMessage (newFeed);
					}
				}
				if (dequeued.GetType () == typeof(PingFeed)) {
					bool returnPing = false;
					RemoteComputerPing = (PingFeed)dequeued;
					returnPing |= RemoteComputerPing.PingRate < Single.Epsilon;
					RemoteComputerPing.PingRate = Math.Abs((RemoteComputerPing.TimeStamp - DateTime.UtcNow).Milliseconds);
					if (returnPing)
						InternalServiceStream.EnqueueMessage (RemoteComputerPing);
				}
			}
		}

		public bool Available ()
		{
			return InternalServiceStream.Avaliable ();
		}

		volatile bool _isActive;
		public bool IsActive {
			get {
				return _isActive;
			}
			set {
				_isActive = value;
			}
		}

		#endregion

		public class CPUFeed
		{
			public float CpuTick { get; set;}

			public CPUFeed()
			{
				CpuTick = 0f;
			}

			public CPUFeed(float cpu)
			{
				CpuTick = cpu;
			}
		}

		public class PingFeed
		{
			public DateTime TimeStamp {get;set;}
			public double PingRate {get;set;}

			public PingFeed()
			{
				PingRate = 0;
				TimeStamp = DateTime.UtcNow;
			}

			public PingFeed(DateTime current, double ping)
			{
				PingRate = ping;
				TimeStamp = current;
			}
		}
	}
}

