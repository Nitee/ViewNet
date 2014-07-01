namespace ViewNet
{
	public class RRegister
	{
		public string Name { get; set;}
		public string Title { get; set;}
		public RegisterFlag Flag {get;set;}

		public enum RegisterFlag
		{
			Success,
			Existed,
			Rejected
		}
	}
}

