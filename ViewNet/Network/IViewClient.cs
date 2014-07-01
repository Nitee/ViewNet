
namespace ViewNet
{
	interface IViewClient
	{
		bool IsConnected { get; }

		void Stop ();

		int CountRecievedPacket ();

		int CountSendingPacket ();

		Packet DequeueRetrievedPacket ();

		void EnqueueSendingPacket (Packet enqueuedPacket);
	}
}

