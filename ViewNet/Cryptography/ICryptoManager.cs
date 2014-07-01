namespace ViewNet
{
	/// <summary>
	/// Standard Interface for CryptoManager
	/// </summary>
	interface ICryptoManager
	{
		void Start ();

		void NewPublicKey ();

		void Stop ();

		void SendMessage (byte[] content);

		int AvailablePackets ();

		byte[] RetrievePacket ();

		bool IsRunning {get;}
	}
}

