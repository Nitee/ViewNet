using System.IO;

namespace ViewNet
{
	public class DirectoryPermission
	{
		public DirectoryInfo DirectoryInformation { get; set; }

		public bool CanRead { get; set; }

		public bool CanWrite { get; set; }

		public bool CanExecute { get; set; }

		public bool CanMakeNewDirectory { get; set; }

		public bool ApplyToSubDirectories { get; set; }

		public DirectoryPermission (DirectoryInfo dir,
		                            bool read,
		                            bool write,
		                            bool execute,
		                            bool cancreatedir,
		                            bool applytosubdir)
		{
			DirectoryInformation = dir;
			CanRead = read;
			CanWrite = write;
			CanExecute = execute;
			CanMakeNewDirectory = cancreatedir;
			ApplyToSubDirectories = applytosubdir;
		}
	}
}

