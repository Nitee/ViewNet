using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.IO;

namespace ViewNet
{
	/// <summary>
	/// The ViewNet Client
	/// </summary>
	class ViewTCPClient : IViewClient
	{

		#region Private Properties

		TcpClient Client { get; set; }

		volatile bool _isConnected;

		Thread _networkThread { get; set; }

		volatile bool _isRunning;

		IPEndPoint _RemoteEndPoint { get; set; }

		Queue<Packet> PacketsToSend { get; set; }

		Queue<Packet> RecievedPackets { get; set; }

		MemoryStream StoredNetStream { get; set; }

		#endregion

		#region Public Properties

		public bool IsConnected {
			get {
				return _isConnected;
			}
		}

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="ViewNet.ViewTCPClient"/> class.
		/// </summary>
		/// <param name="ip">Ip.</param>
		/// <param name="port">Port.</param>
		public ViewTCPClient (IPAddress ip, ushort port)
		{
			Client = new TcpClient ();
			if (port == 0)
				throw new Exception ("Port must be within 1 - 65535, not a 0!");
			Client.Connect (ip, port);
			InitializeObjects ();
			_RemoteEndPoint = new IPEndPoint (ip, port);
			Start ();
		}
			
		/// <summary>
		/// Initializes a new instance of the <see cref="ViewNet.ViewTCPClient"/> class.
		/// </summary>
		/// <param name="tcpclient">Tcpclient.</param>
		public ViewTCPClient (TcpClient tcpclient)
		{
			Client = tcpclient;
			if (!tcpclient.Connected)
				throw new Exception ("TcpClient must be connected before constructing the ViewNet client.");
			InitializeObjects ();
			_RemoteEndPoint = (IPEndPoint)tcpclient.Client.RemoteEndPoint;
			Start ();
		}

		void InitializeObjects ()
		{
			_isConnected = true;
			PacketsToSend = new Queue<Packet> ();
			RecievedPackets = new Queue<Packet> ();
			StoredNetStream = new MemoryStream ();
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Start the ViewNet Client and the Network Thread
		/// </summary>
		public void Start ()
		{
			if (_isRunning)
				return;
			lock (Client) {
				if (!Client.Connected)
					Client.Connect (_RemoteEndPoint);
			}
			_networkThread = new Thread (ThreadProcess);
			_networkThread.Name = "ViewTCPClient Thread";
			_isRunning = true;
			_networkThread.Start ();
		}

		/// <summary>
		/// Stop the ViewNet Client and the Network Thread
		/// </summary>
		public void Stop ()
		{
			lock (Client) {
				if (!_isRunning)
					return;
				_isRunning = false;
				Client.Close ();
				// Clear for next connection.
				RecievedPackets.Clear ();
				PacketsToSend.Clear ();
				StoredNetStream.Close ();
				StoredNetStream.Dispose ();
				StoredNetStream = new MemoryStream ();
			}
		}

		/// <summary>
		/// Counts the recieved packet in the queue
		/// </summary>
		/// <returns>Number of recieved packets</returns>
		public int CountRecievedPacket ()
		{
			int iCount;
			lock (RecievedPackets) {
				iCount = RecievedPackets.Count;
			}
			return iCount;
		}

		/// <summary>
		/// Counts the sending packet in the queue
		/// </summary>
		/// <returns>Number of sending packets</returns>
		public int CountSendingPacket ()
		{
			int iCount;
			lock (PacketsToSend) {
				iCount = PacketsToSend.Count;
			}
			return iCount;
		}

		/// <summary>
		/// Dequeues the retrieved packet
		/// </summary>
		/// <returns>The retrieved packet.</returns>
		public Packet DequeueRetrievedPacket ()
		{
			Packet dequeuedPacket;
			lock (RecievedPackets)
				dequeuedPacket = RecievedPackets.Dequeue ();
			return dequeuedPacket;
		}

		/// <summary>
		/// Enqueues the sending packet
		/// </summary>
		/// <param name="enqueuedPacket">Enqueued packet.</param>
		public void EnqueueSendingPacket (Packet enqueuedPacket)
		{
			lock (PacketsToSend)
				PacketsToSend.Enqueue (enqueuedPacket);
		}

		#endregion

		#region Private Methods

		void ThreadProcess ()
		{
			bool FindLength = true;
			int packetLength = -1;
			var lengthBytes = new byte[4];
			int recievedData = 0;
			var contentBytes = new byte[0];
			while (_isRunning) {
				lock (Client) {
					var NetStream = Client.GetStream ();
					if (NetStream.DataAvailable) {
						if (FindLength) {
							recievedData += NetStream.Read (lengthBytes, recievedData, lengthBytes.Length - recievedData);
							if (recievedData == 4) {
								FindLength = false;
								packetLength = BitConverter.ToInt32 (lengthBytes, 0);
								recievedData = 0;
								contentBytes = new byte[packetLength];
							}
						} else {
							recievedData += NetStream.Read (contentBytes, recievedData, contentBytes.Length - recievedData);
							if (recievedData == packetLength) {
								lock (RecievedPackets) {
									RecievedPackets.Enqueue (Packet.DeserializePacket (contentBytes));
									FindLength = true;
									recievedData = 0;
									packetLength = -1;
								}
							}
						}
					}
					lock (PacketsToSend)
						if (PacketsToSend.Count > 0) {
							var sendIt = PacketsToSend.Dequeue ();
							try {
								byte[] sendingData = sendIt.SerializePacket ();
								var lengthExport = BitConverter.GetBytes (sendingData.Length);
								NetStream.Write (lengthExport, 0, lengthExport.Length);
								NetStream.Write (sendingData, 0, sendingData.Length);
							} catch (Exception ex) {
								System.Diagnostics.EventLog.WriteEntry ("ViewClient", ex.Message);
								Stop ();
							}
						}
				}
				Thread.Sleep (0);
			}
			_isRunning = false;
		}

		#endregion

	}
}

