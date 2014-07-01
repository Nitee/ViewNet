namespace ViewNet
{
	public class MLogin
	{
		public MLogin ()
		{
			Name = string.Empty;
			Auth = new byte[64];
		}

		public MLogin (string name, byte[] auth)
		{
			Name = name;
			Auth = auth;
		}

		public string Name { get; set; }

		public byte[] Auth { get; set; }
	}
}

