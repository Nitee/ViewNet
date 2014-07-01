namespace ViewNet
{
	public class RBlacklisted
	{
		public string Message {get;set;}

		public RBlacklisted ()
		{
			Message = "Your connection is now blacklisted.";
		}

		public RBlacklisted(string message)
		{
			Message = message;
		}
	}
}

