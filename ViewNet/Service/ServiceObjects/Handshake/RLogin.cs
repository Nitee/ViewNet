namespace ViewNet
{
	public class RLogin
	{
		public string Name {get;set;}

		public uint Retry {get;set;}

		public ulong NextGreetID { get; set; }

		public LoginState LoginFlag { get; set; }

		public RLogin ()
		{
			Name = string.Empty;
			Retry = 0;
			NextGreetID = 0;
			LoginFlag = LoginState.NotAuthenicated;
		}

		public RLogin(string name, uint retries, ulong nextgreet, LoginState login)
		{
			Name = name;
			Retry = retries;
			NextGreetID = nextgreet;
			LoginFlag = login;
		}

		public enum LoginState
		{
			Authenicated,
			NotAuthenicated
		}
	}
}

