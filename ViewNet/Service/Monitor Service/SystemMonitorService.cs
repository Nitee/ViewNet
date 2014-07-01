using System;
using System.Diagnostics;
using System.Collections.Generic;
namespace ViewNet.Service
{
	public class SystemMonitorService : IService
	{
		private PerformanceCounter performanceCounterCPU;
		private DateTime currentTime = DateTime.UtcNow;
		private ByteServiceStream serviceStream = new ByteServiceStream();
		public bool IsActive {
		get {
			return _IsActive;
		}
		set {
			_IsActive = value;	
		}
	}
		private volatile bool _IsActive = false;
		/// <summary>
		/// Gets or sets the tick duration to feed update to remote computer of your
		/// computer status
		/// </summary>
		/// <value>The tick.</value>
		public double Tick {
			get {
				return tick;
			}
			set {
				tick = value;
			}
		}

		private double tick = 500;
		public CPUFeed CPUTick {get;set;}
		public class CPUFeed
		{
			public float CPU { get; set; }
		}
		private CPUFeed ToRemoteFeed {get;set;}

		public SystemMonitorService ()
		{
			performanceCounterCPU = new PerformanceCounter ("Processor",
			                                               "% Processor Time",
			                                               "_Total");
			CPUTick = new CPUFeed ();
			ToRemoteFeed = new CPUFeed ();
			ToRemoteFeed.CPU = 0.0f;
			CPUTick.CPU = 0.0f;
			Tick = 500;
		}

		public byte[] Write()
		{
			byte[] feed = new byte[4];
			Array.Copy (BitConverter.GetBytes (performanceCounterCPU.NextValue ()),
			           feed, 4);
			ToRemoteFeed.CPU
			return feed;
		}

		public void Read(byte[] data)
		{
			if (data.Length != 4)
				return;

			cpu = BitConverter.ToSingle (data, 0);
		}

		public bool Available()
		{
			if ((DateTime.UtcNow - currentTime).TotalMilliseconds >= Tick) {
				currentTime = DateTime.UtcNow;
				return true;
			}
			return false;
		}
	}
}

