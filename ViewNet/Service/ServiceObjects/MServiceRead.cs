namespace ViewNet
{
	public class MServiceRead
	{
		public MServiceRead ()
		{
		}

		public MServiceRead (ulong id, byte[] data)
		{
			ID = id;
			Data = data;
		}

		public ulong ID { get; set; }

		public byte[] Data { get; set; }
	}
}

