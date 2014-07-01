using System.IO;

namespace ViewNet
{
	class Packet
	{
		public PacketType TypeOfPacket { get; set; }

		public byte[] Content { get; set; }

		public Packet (PacketType typeOfPacket, byte[] content)
		{
			TypeOfPacket = typeOfPacket;
			Content = content;
		}

		public byte[] SerializePacket ()
		{
			var memWrite = new MemoryStream ();
			memWrite.WriteByte ((byte)TypeOfPacket);
			DataUtility.WriteOnlyBytesToStream (memWrite, Content);
			return memWrite.ToArray ();
		}

		public static Packet DeserializePacket (byte[] input)
		{
			var inputStream = new MemoryStream (input);
			var pt = (PacketType)inputStream.ReadByte ();
			byte[] content = DataUtility.ReadOnlyBytesFromStream (inputStream, input.Length - 1);
			return new Packet (pt, content);
		}
	}
}

