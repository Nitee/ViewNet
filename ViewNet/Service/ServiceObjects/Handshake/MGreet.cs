namespace ViewNet
{
	/// <summary>
	/// Server side greeting
	/// </summary>
	public class MGreet
	{
		public MGreet ()
		{
			ID = 0;
			Version = "";
		}

		public MGreet(ulong id, string version)
		{
			ID = id;
			Version = version;
		}

		public ulong ID {get;set;}

		public string Version {get;set;}
	}
}

