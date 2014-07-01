using System.IO;
namespace ViewNet
{
	public class FilePermission
	{
		public FileInfo FileInformation {get;set;}
		public bool CanRead {get;set;}
		public bool CanWrite {get;set;}
		public bool CanExecute {get;set;}
		public FilePermission (FileInfo file, bool read, bool write, bool execute)
		{
			FileInformation = file;
			CanRead = read;
			CanWrite = write;
			CanExecute = execute;
		}
	}
}