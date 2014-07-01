using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace ViewNet
{
	class DHToAES256Manager : ICryptoManager
	{
		#region Working Objects
		readonly Rijndael Rij = Rijndael.Create ();

		IViewClient client { get; set; }

		ConcurrentQueue<byte[]> DecryptedNormalPackets = new ConcurrentQueue<byte[]> ();
		volatile bool _IsRunning;

		Thread cryptoThread { get; set; }

		ConcurrentQueue<byte[]> SendingPackets = new ConcurrentQueue<byte[]> ();
		DateTime CheckForKeyDate = DateTime.UtcNow;
		volatile bool FirstExchange;
		volatile bool IsHost;
		volatile bool MiddleOfTransition;
		volatile bool ChangingKey;
		/// <summary>
		/// The crypto cycle.
		/// 0 = Key is not exchanged...
		/// 1 = DH is exchanged only once. ALL OTHER PACKETS THAN DH EXCHANGES ARE FORBIDDEN.
		/// 2 = Second DH exchange is encrypted and allowing minimum security. All packets are permitted for exchanges.
		/// </summary>
		int CryptoCycle;
		/// <summary>
		/// The Diffie-Hellman Module.
		/// This is the primary means of exchanging new encryption keys across the network.
		/// </summary>
		DiffieHellman CoreDH = new DiffieHellman ();
		/// <summary>
		/// First Send Boolean
		/// </summary>
		volatile bool SendTheNewKeyNow;
		#endregion

		public bool IsRunning {
			get {
				return _IsRunning;
			}
		}


		public DHToAES256Manager (IPEndPoint ipendpoint)
		{
			client = new ViewTCPClient (ipendpoint.Address, (ushort)ipendpoint.Port);
		}

		public DHToAES256Manager (TcpClient tcpClient)
		{
			client = new ViewTCPClient (tcpClient);
			IsHost = true;
			FirstExchange = true;
		}

		#region Public Methods

		/// <summary>
		/// Start this instance.
		/// </summary>
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
			cryptoThread.Name = "Crypto Thread";
			cryptoThread.Start ();
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
			if (content.Length == 0)
				return;
			SendingPackets.Enqueue (content);
		}

		/// <summary>
		/// Get the count of availables packets.
		/// </summary>
		/// <returns>The packets.</returns>
		public int AvailablePackets ()
		{
			int countOfPackets;
			countOfPackets = DecryptedNormalPackets.Count;
			return countOfPackets;
		}

		/// <summary>
		/// Retrieves the packet.
		/// </summary>
		/// <returns>The packet.</returns>
		public byte[] RetrievePacket ()
		{
			byte[] info;
			DecryptedNormalPackets.TryDequeue (out info);
			return info;
		}

		/// <summary>
		/// Allow host to invoke a request to initiate a new key exchange
		/// </summary>
		public void NewPublicKey ()
		{
			if (!IsHost)
				return;
			SendNewKeyRequest ();
		}
		#endregion

		#region Private Methods

		void ThreadProcess ()
		{
			try {
				while (_IsRunning &&
				       client.IsConnected) {
					Process ();
					Thread.Sleep (1);
				}
			} catch (Exception) {
				Stop ();
			}
		}

		void SetToNewCryptoKey ()
		{
			if (CoreDH.Key.Length < 32) {
				if (IsHost)
					SendNewKeyRequest ();
				return;
			}
			byte[] NewKeyToUse = SpecialXORIficateTheKey (CoreDH.Key);
			Rij.Key = NewKeyToUse;
			if (CryptoCycle < 2) // Incase if someone had to exchange key more times than there are in Int32...
				CryptoCycle++;
		}

		static byte[] SpecialXORIficateTheKey (byte[] massBuffer)
		{
			if (massBuffer.Length <= 32)
				return massBuffer;

			var Mainbuffer = new byte[32];
			Array.Copy (massBuffer, Mainbuffer, 32);
			int current = 0;
			for (int I = 32; I < massBuffer.Length; I++) {
				Mainbuffer [current] ^= massBuffer [I];
				current++;
				if (current == 32)
					current = 0;
			}
			return Mainbuffer;
		}

		Rijndael GetTemporaryNewCrypto ()
		{
			if (CoreDH.Key.Length < 32)
				return Rij;
			byte[] NewKeyToUse = SpecialXORIficateTheKey (CoreDH.Key);
			Rijndael tempCrypto = Rijndael.Create ();
			tempCrypto.Key = NewKeyToUse;

			return tempCrypto;
		}

		void Process ()
		{
			Packet newPacket;
			lock (client) {
				// Send a new request for new key
				if (CheckForKeyDate.AddMinutes (5) < DateTime.UtcNow && CryptoCycle >= 2 && !ChangingKey && IsHost ||
				    SendTheNewKeyNow) {
					SendNewKeyRequest ();
					CheckForKeyDate = DateTime.UtcNow;
					SendTheNewKeyNow = false;
					ChangingKey = true;
				}

				if (FirstExchange && IsHost) {
					SendFirstKeyRequest ();
					FirstExchange = false;
				}
				// Recieve Process
				if (client.CountRecievedPacket () > 0) {

					// Pop the Packet, prepare the byte array... then check against the identifier of that packet...
					newPacket = client.DequeueRetrievedPacket ();
					switch (newPacket.TypeOfPacket) {
					case PacketType.Normal:
						{
							if (CryptoCycle < 2) {
								// Software Policy Violation. Disconnect.
								Stop ();
								return;
							}
							DecryptedNormalPackets.Enqueue (DecryptBytes (newPacket.Content));
							break;
						}
					case PacketType.NewKey:
						{
							try {
								// Only host is allowed to do this, if sent by Client, AUTODISCONNECT!
								if (IsHost){
									Stop();
									return;
								}
								if (CryptoCycle == 0) {
									var sendTo = CoreDH.GenerateResponse (newPacket.Content);
									if (CoreDH.Key.Length < 32)
										throw new Exception ();
									client.EnqueueSendingPacket (new Packet (PacketType.ReplyExchange, sendTo));
								} else {
									var sendTo = CoreDH.GenerateResponse (DecryptBytes (newPacket.Content));
									if (CoreDH.Key.Length < 32)
										throw new Exception ();
									client.EnqueueSendingPacket (new Packet (PacketType.ReplyExchange, EncryptBytes (sendTo)));
								}
							} catch (Exception) {
								client.EnqueueSendingPacket (new Packet (PacketType.KeyResponseError, new byte[0]));
								break;
							}
							MiddleOfTransition = true;
							break;
						}

					case PacketType.ReplyExchange:
						{
							if (CryptoCycle == 0) {
								CoreDH.HandleResponse (newPacket.Content);
							} else {
								CoreDH.HandleResponse (DecryptBytes (newPacket.Content));
							}

							client.EnqueueSendingPacket (new Packet (PacketType.NotifyEnc, new byte[0]));
							SetToNewCryptoKey ();
							ChangingKey = false;
							if (CryptoCycle < 2) {
								SendNewKeyRequest ();
							}
							break;
						}

					case PacketType.NotifyEnc:
						{
							MiddleOfTransition = false;
							SetToNewCryptoKey ();
							break;
						}

					// DH can fail, so this is made for fixing this error by
					// regenerating new DH key.
					case PacketType.KeyResponseError:
						{
							if (CryptoCycle == 0) {
								SendFirstKeyRequest ();
							} else {
								SendNewKeyRequest ();
							}

							break;
						}

					default:
						{
							Stop (); // Software Policy Violation. DISCONNECT ALL THE THINGS!
							break;
						}
					}
				}

				if (CryptoCycle >= 2 && SendingPackets.Count > 0) {
					byte[] content;
					bool successCheck = SendingPackets.TryDequeue (out content);

					if (!successCheck)
						return;
					if (MiddleOfTransition) {
						client.EnqueueSendingPacket (new Packet (PacketType.Normal, TempEncryptBytes (GetTemporaryNewCrypto (), content)));
					} else {
						client.EnqueueSendingPacket (new Packet (PacketType.Normal, EncryptBytes (content)));
					}
				}
			}
		}

		#endregion

		void SendFirstKeyRequest ()
		{
			var firstPub = CoreDH.GenerateRequest ();
			client.EnqueueSendingPacket (new Packet (PacketType.NewKey, firstPub));
			ChangingKey = true;
		}

		void SendNewKeyRequest ()
		{
			var newReq = CoreDH.GenerateRequest ();
			client.EnqueueSendingPacket (new Packet (PacketType.NewKey, EncryptBytes (newReq)));
			ChangingKey = true;
		}

		byte[] EncryptBytes (byte[] message)
		{
			if ((message == null) || (message.Length == 0)) {
				return message;
			}
			Rij.GenerateIV ();
			using (var outStream = new MemoryStream ()) {
				DataUtility.WriteBytesToStream (Rij.IV, outStream);
				DataUtility.WriteBytesToStream (Rij.CreateEncryptor ().TransformFinalBlock (message, 0, message.Length), outStream);
				return outStream.ToArray ();
			}
		}

		static byte[] TempEncryptBytes (SymmetricAlgorithm crypto, byte[] message)
		{
			if ((message == null) || (message.Length == 0)) {
				return message;
			}
			crypto.GenerateIV ();
			using (var outStream = new MemoryStream ()) {
				DataUtility.WriteBytesToStream (crypto.IV, outStream);
				DataUtility.WriteBytesToStream (crypto.CreateEncryptor ().TransformFinalBlock (message, 0, message.Length), outStream);
				return outStream.ToArray ();
			}
		}

		byte[] DecryptBytes (byte[] message)
		{
			if ((message == null) || (message.Length == 0)) {
				return message;
			}
			byte[] restOfData;
			using (var inStream = new MemoryStream (message)) {
				Rij.IV = DataUtility.ReadBytesFromStream (inStream);
				restOfData = DataUtility.ReadBytesFromStream (inStream);
			}
			var result = Rij.CreateDecryptor ().TransformFinalBlock (restOfData, 0, restOfData.Length);
			return result;
		}
	}
}

