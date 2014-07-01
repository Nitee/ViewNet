using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.IO;

namespace ViewNet
{
	public class RelayService : IService
	{
		TcpListener listen { get; set; }
		public bool IsActive {
		get {
			return _IsActive;
		}
		set {
			_IsActive = value;	
		}
	}
		volatile bool _IsActive;
		TcpClient connectedClient;
		TcpClient ConnectTo;
		volatile bool Initialized;
		volatile bool IsHost;
		readonly Queue<byte[]> SendQueries = new Queue<byte[]> ();
		Queue<byte[]> RecieveQueries = new Queue<byte[]> ();
		Thread coreThread;

		public RelayService (IPEndPoint bindEndPoint, IPEndPoint remoteEndPoint, bool isHosting)
		{
			IsHost = isHosting;
			if (isHosting) {
				listen = new TcpListener (bindEndPoint);
				listen.Start ();
			} else {
				ConnectTo = new TcpClient (bindEndPoint);
			}
			SendQueries.Enqueue (WriteRequest (remoteEndPoint));
			Initialized = true;
			coreThread = new Thread (ThreadProcess);
			coreThread.Start ();
		}

		public RelayService ()
		{

		}

		byte[] WriteRequest (IPEndPoint remoteEndPoint)
		{
			using (var outStream = new MemoryStream ()) {
				if (IsHost) {
					outStream.WriteByte ((byte)IdentifyRelay.Connect);
				} else {
					outStream.WriteByte ((byte)IdentifyRelay.ListenFor);
				}
				DataUtility.WriteStringToStream (remoteEndPoint.Address.ToString (), outStream);
				DataUtility.WriteInt32ToStream (remoteEndPoint.Port, outStream);
				return outStream.ToArray ();
			}
		}

		public byte[] Write ()
		{
			byte[] rawData;
				lock (SendQueries)
					rawData = SendQueries.Dequeue ();
				return rawData;
		}

		public void Read (byte[] data)
		{
			var instream = new MemoryStream (data);
			var identify = (IdentifyRelay)instream.ReadByte ();
			switch (identify) {
			case IdentifyRelay.Connect:
				{
					var addr = new IPEndPoint (IPAddress.Parse (DataUtility.ReadStringFromStream (instream)),
					                           DataUtility.ReadInt32FromStream (instream));
					ConnectTo = new TcpClient ();
					ConnectTo.Connect (addr);
					Initialized = true;
					coreThread = new Thread (ThreadProcess);
					coreThread.Start ();
					break;
				}

			case IdentifyRelay.ListenFor:
				{
					var addr = new IPEndPoint (IPAddress.Parse (DataUtility.ReadStringFromStream (instream)),
					                          DataUtility.ReadInt32FromStream (instream));
					listen = new TcpListener (addr);
					listen.Start ();
					Initialized = true;
					coreThread = new Thread (ThreadProcess);
					coreThread.Start ();
					break;
				}

			case IdentifyRelay.Relay:
				{
					var rawdata = DataUtility.ReadBytesFromStream (instream);
					lock (RecieveQueries)
						RecieveQueries.Enqueue (rawdata);
					break;
				}
			}
		}

		void ThreadProcess ()
		{
			while (_IsActive) {
				if (IsHost && Initialized) {
					ProcessForServer ();
				} else {
					ProcessForClient ();
				}
				Thread.Sleep (1);
			}
		}

		void ProcessForServer ()
		{
			if (listen.Pending ())
				connectedClient = listen.AcceptTcpClient ();
			if (connectedClient == null)
				return;
			if (connectedClient.Available > 0) {
				byte[] data;
				data = new byte[8192];
				int index = connectedClient.GetStream ().Read (data, 0, data.Length);
				Array.Resize (ref data, index);
				var outstream = new MemoryStream ();
				outstream.WriteByte ((byte)IdentifyRelay.Relay);
				DataUtility.WriteBytesToStream (data, outstream);
				lock (SendQueries)
					SendQueries.Enqueue (outstream.ToArray ());
				outstream.Dispose ();
			}

			byte[] rawdata = null;
			lock (RecieveQueries) {
				if (RecieveQueries.Count > 0) {
					rawdata = RecieveQueries.Dequeue ();
				}
			}
			if (rawdata != null) {
				connectedClient.GetStream ().Write (rawdata, 0, rawdata.Length);
			}
		}

		void ProcessForClient ()
		{
			// if server is sending the client data, then transmit to the relay
			if (ConnectTo.Available > 0) {
				var data = new byte[8192];
				int limiter = ConnectTo.GetStream ().Read (data, 0, data.Length);
				Array.Resize (ref data, limiter);
				var outstream = new MemoryStream ();
				outstream.WriteByte ((byte)IdentifyRelay.Relay);
				DataUtility.WriteBytesToStream (data, outstream);
				lock (SendQueries)
					SendQueries.Enqueue (outstream.ToArray ());
				outstream.Dispose ();
			}

			// If recieve data, write it out to stream
			byte[] rawdata = null;
			lock (RecieveQueries) {
				if (RecieveQueries.Count > 0) {
					rawdata = RecieveQueries.Dequeue ();
				}
			}
			if (rawdata != null) {
				ConnectTo.GetStream ().Write (rawdata, 0, rawdata.Length);
			}
		}

		public bool Available ()
		{
			int count;
			lock (SendQueries)
				count = SendQueries.Count;
			return count > 0;
		}

		enum IdentifyRelay
		{
			Connect,
			ListenFor,
			Relay
		}
	}
}

