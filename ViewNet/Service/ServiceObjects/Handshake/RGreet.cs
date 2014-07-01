namespace ViewNet
{
	public class RGreet
	{
		public ulong Retries { get; set; }

		public ConnectionState State { get; set; }

		public byte[] SHAConnectionAuth { get; set;}

		public string ServerName {get;set;}

		public RGreet ()
		{
			Retries = 0;
			State = ConnectionState.ConnectionInvalid;
			SHAConnectionAuth = new byte[64];
			ServerName = string.Empty;
		}

		public RGreet(ulong retries, ConnectionState state)
		{
			Retries = retries;
			State = state;
			SHAConnectionAuth = new byte[64];
			ServerName = string.Empty;
		}

		public RGreet(ulong retries, ConnectionState state, byte[] auth, string servername)
		{
			Retries = retries;
			State = state;
			SHAConnectionAuth = auth;
			ServerName = servername;
		}

		public enum ConnectionState
		{
			RegistrationAccepted,
			RegistrationRejected,
			ConnectionAuthenicated,
			ConnectionInvalid,
			WrongVersion
		}
	}
}

