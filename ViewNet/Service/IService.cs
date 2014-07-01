namespace ViewNet
{
	public interface IService
	{
		bool IsActive {get;set;}
		byte[] Write();
		void Read(byte[] data);
		bool Available();
	}
}