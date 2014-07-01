using System.Collections.Generic;
namespace ViewNet
{
	public class Group
	{
		public string Title {get;set;}
		public Dictionary<string, Permission> GroupPermission {get;set;}
		public Group ()
		{
			Title = string.Empty;
			GroupPermission = new Dictionary<string, Permission> ();
		}
	}
}

