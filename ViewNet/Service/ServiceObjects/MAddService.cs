namespace ViewNet
{
	public class MAddService
	{
		public MAddService ()
		{
		}

		public MAddService (ulong id, string name)
		{
			ID = id;
			FullName = name;
		}

		public ulong ID { get; set; }

		public string FullName { get; set; }
	}
}

