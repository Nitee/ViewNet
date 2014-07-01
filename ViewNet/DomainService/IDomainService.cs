using System.Net;
namespace ViewNet
{
	public interface IDomainService
	{
		IService[] AddConnection (IPEndPoint ip);
		void RemoveConnection(IPEndPoint ip);
		Permission[] GetDomainServicePermission ();
	}
}

