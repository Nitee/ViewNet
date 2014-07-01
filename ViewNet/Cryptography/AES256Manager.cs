using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ViewNet
{
	/// <summary>
	/// An Interface Manager for ViewNet Client and Server
	/// Do not use, this is not a secure form of communication and is a precursor to DHToAES256Manager
	/// </summary>
	class AES256Manager : ICryptoManager
	{
		// A pre-shared private key for encryption purpose
		byte[] PrivateKey { get; set; }
		// Old Public Key
		byte[] PublicKeyO { get; set; }
		// New Public Key
		byte[] PublicKeyN { get; set; }

		Rijndael Rij { get; set; }

		IViewClient client { get; set; }

		Queue<byte[]> DecryptedNormalPackets { get; set; }

		volatile bool _IsRunning;

		Thread cryptoThread { get; set; }

		readonly Queue<byte[]> _sendingPackets = new Queue<byte[]>();

		public Queue<byte[]> SendingPackets {
			get {
				return _sendingPackets;
			}
		}

		volatile bool FirstGreet;

		readonly DateTime CheckForKeyDate;

		public bool IsRunning {
			get {
				return _IsRunning;
			}
		}

		public AES256Manager (IPEndPoint ipendpoint, byte[] privatekey)
		{
			//if (UseUDP) {
			//	throw new NotImplementedException ();
				//client = new ViewUdpClient (ipendpoint.Address, (ushort)ipendpoint.Port);
			//} else {
			client = new ViewTCPClient (ipendpoint.Address, (ushort)ipendpoint.Port);
			//}
			DecryptedNormalPackets = new Queue<byte[]> ();
			InitializeEncryption (privatekey);
			SendMessage (Rij.IV);
			FirstGreet = true;
			CheckForKeyDate = DateTime.UtcNow;
		}

		public AES256Manager (TcpClient tcpClient, byte[] privatekey)
		{
			InitializeEncryption (privatekey);
			client = new ViewTCPClient (tcpClient);
			DecryptedNormalPackets = new Queue<byte[]> ();
			CheckForKeyDate = DateTime.UtcNow;
		}

		/*
		public AES256Manager (UdpClient udpClient, byte[] privatekey)
		{
			throw new NotImplementedException ();
			InitializeEncryption (privatekey);
			//client = new ViewUdpClient (udpClient);
			DecryptedNormalPackets = new Queue<byte[]> ();
			SendingPackets = new Queue<byte[]> ();
			CheckForKeyDate = DateTime.UtcNow;
		}
*/
		void InitializeEncryption(byte[] privateKey)
		{
			PrivateKey = new byte[privateKey.Length];
			privateKey.CopyTo (PrivateKey, 0);
			Rij = Rijndael.Create ();
			Rij.GenerateIV ();
			PublicKeyO = Rij.IV;
			PrivateKey = new byte[privateKey.Length];
			Rij.Key = PrivateKey;
		}
		#region Public Methods
		public void Start ()
		{
			if (_IsRunning)
				return;

			if (cryptoThread != null) {
				if (cryptoThread.ThreadState == ThreadState.Running)
					return;
			}
			_IsRunning = true;
			cryptoThread = new Thread (ThreadProcess);
			cryptoThread.Start ();
		}

		public void NewPublicKey ()
		{
			lock (PublicKeyO) {
				if (PublicKeyO == null)
					return;
				Rij.GenerateIV ();
				PublicKeyN = new byte[Rij.IV.Length];
				Rij.IV.CopyTo (PublicKeyN, 0);
				Rij.IV = PublicKeyO;
				byte[] encryptKey = Rij.CreateEncryptor ().TransformFinalBlock (PublicKeyN, 0, PublicKeyN.Length);
				lock (client)
					client.EnqueueSendingPacket (new Packet (PacketType.NewKey, encryptKey));
			}
		}

		/// <summary>
		/// Stop this instance.
		/// </summary>
		public void Stop ()
		{
			if (!_IsRunning)
				return;
			_IsRunning = false;
			lock (client)
				client.Stop ();
		}

		/// <summary>
		/// Sends the message.
		/// </summary>
		/// <param name="content">Content.</param>
		public void SendMessage (byte[] content)
		{
			lock (_sendingPackets)
				_sendingPackets.Enqueue (content);
		}

		/// <summary>
		/// Get Availables packets.
		/// </summary>
		/// <returns>The packets.</returns>
		public int AvailablePackets ()
		{
			int countOfPackets;
			lock (DecryptedNormalPackets) {
				countOfPackets = DecryptedNormalPackets.Count;
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
			lock (DecryptedNormalPackets) {
				info = DecryptedNormalPackets.Dequeue ();
			}
			return info;
		}
		#endregion
		#region Private Methods
		void ThreadProcess ()
		{
			while (_IsRunning &&
			       client.IsConnected) {
				Process ();
				Thread.Sleep (1);
			}
		}

		void Process ()
		{
			Packet newPacket;
			bool keyChange = false;
			lock (client) {
				if (client.CountRecievedPacket () > 0) {

					newPacket = client.DequeueRetrievedPacket ();
					byte[] DecryptedPacket;
					switch (newPacket.TypeOfPacket) {
					case PacketType.Normal:
						{
							Rij.IV = PublicKeyO;
							Rij.Key = PrivateKey;
							DecryptedPacket = Rij.CreateDecryptor ().TransformFinalBlock
					(newPacket.Content, 0, newPacket.Content.Length);
							DecryptedNormalPackets.Enqueue (DecryptedPacket);
							break;
						}
					case PacketType.NewKey:
						{
							PublicKeyN = Rij.CreateDecryptor ().TransformFinalBlock
					(newPacket.Content, 0, newPacket.Content.Length);
							keyChange = true;
							break;
						}

					case PacketType.NormalEnc:
						{
							Rij.IV = PublicKeyO;
							Rij.Key = PrivateKey;
							DecryptedPacket = Rij.CreateDecryptor ().TransformFinalBlock
						(newPacket.Content, 0, newPacket.Content.Length);
							DecryptedNormalPackets.Enqueue (DecryptedPacket);
							PublicKeyN.CopyTo (PublicKeyO, 0);
							PublicKeyN = null;
							break;
						}
					case PacketType.Greet:
						{
							// First public key exchange
							PublicKeyO = newPacket.Content;
							break;
						}

					default:
						{
							Stop ();
							break;
						}
					}
				}
				// Must have public key for the crypto connection to works!
				if (PublicKeyO == null) {
					// If fail to get the public key for the encryption, then shut down the connection
					if ((DateTime.UtcNow - CheckForKeyDate).Seconds > 10)
						Stop ();
					return;
				}
				byte[] content;
				lock (_sendingPackets) {
					if (_sendingPackets.Count > 0) {
						content = _sendingPackets.Dequeue ();
					} else
						return;
				}
				PacketType typeToUse;
				if (FirstGreet) {
					typeToUse = PacketType.Greet;
				} else if (!keyChange) {
					typeToUse = PacketType.Normal;
				} else {
					typeToUse = PacketType.NormalEnc;
				}
				if (!FirstGreet) {
					Rij.IV = PublicKeyO;
					content = Rij.CreateEncryptor ().TransformFinalBlock (content, 0, content.Length);
				} else
					FirstGreet = false;
				client.EnqueueSendingPacket (new Packet (typeToUse, content));
			}
		}
		#endregion
	}
}

