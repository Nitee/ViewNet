using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
namespace ViewNet
{

	/// <summary>
	/// No Encryption Manager. 
	/// Do not use unless you're planning on building a new type of CryptoManager using this as a template.
	/// </summary>
	class NoCryptoManager : ICryptoManager
	{
		Queue<byte[]> recievedNetPackets { get; set; }

		IViewClient client { get; set; }

		Thread innerThreadProcess { get; set; }

		volatile bool _isRunning;
		public bool IsRunning {
			get {
				return _isRunning;
			}
		}

		public NoCryptoManager (TcpClient tcpClient)
		{
			recievedNetPackets = new Queue<byte[]> ();
			client = new ViewTCPClient (tcpClient);
			innerThreadProcess = new Thread (ThreadProcess);
		}

		public NoCryptoManager (IPEndPoint endpoint)
		{
			recievedNetPackets = new Queue<byte[]> ();
			client = new ViewTCPClient (endpoint.Address, (ushort)endpoint.Port);

		}

		public void Start ()
		{
			if (_isRunning)
				return;
			_isRunning = true;
			innerThreadProcess = new Thread (ThreadProcess);
			innerThreadProcess.Start ();
		}

		public void NewPublicKey ()
		{

		}

		public void Stop ()
		{
			if (!_isRunning) {
				return;
			}
			_isRunning = false;
			innerThreadProcess.Join ();
			innerThreadProcess = null;
		}

		public void SendMessage (byte[] content)
		{
			client.EnqueueSendingPacket (new Packet (PacketType.Normal, content));
		}

		/// <summary>
		/// Get Availables packets.
		/// </summary>
		/// <returns>The packets.</returns>
		public int AvailablePackets ()
		{
			int countOfPackets;
			lock (recievedNetPackets) {
				countOfPackets = recievedNetPackets.Count;
			}
			return countOfPackets;
		}

		/// <summary>
		/// Retrieves the packet.
		/// </summary>
		/// <returns>The packet.</returns>
		public byte[] RetrievePacket ()
		{
			byte[] info;
			lock (recievedNetPackets) {
				info = recievedNetPackets.Dequeue ();
			}
			return info;
		}

		void ThreadProcess ()
		{
			while (client.IsConnected &&
			       _isRunning) {
				Process ();
				Thread.Sleep (1);
			}
		}

		void Process ()
		{
			Packet newPacket;
			lock (client) {
				if (client.CountRecievedPacket () > 0) {

					newPacket = client.DequeueRetrievedPacket ();
					if (newPacket.TypeOfPacket != PacketType.Normal) {
						return;
					}
					recievedNetPackets.Enqueue (newPacket.Content);
				}
			}
		}
	}
}

