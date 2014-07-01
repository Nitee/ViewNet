namespace ViewNet
{
	public class Permission
	{
		public string Category { get; set;}
		public string Name { get; set;}
		public bool IsPermitted {get;set;}
		public Permission (string category, string name, bool permitted)
		{
			Category = category;
			Name = name;
			IsPermitted = permitted;
		}
	}
}

