/*
 * Not In Use!
using System;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;
namespace ViewNet
{
	/// <summary>
	/// An inbuilt logging agent reserved for ViewNet.
	/// Currently not being used now.
	/// </summary>
	public class LoggerAgent : IDisposable
	{
		const string LogDirectory = "Logs";

		string NamespaceLog { get; set; }

		string TypeOfLog { get; set; }

		string FileName { get; set; }

		string FilePath { get; set; }

		FileStream IOToFile { get; set; }

		ConcurrentQueue<string> QueuesOfOutput = new ConcurrentQueue<string> ();
		StreamWriter IOWriter {get;set;}

		volatile bool IsActive;

		Thread InternalThread;

		int UniqueOriginatorThreadID;

		public LoggerAgent (string namespaceLog, string typeOfLog)
		{
			UniqueOriginatorThreadID = Thread.CurrentThread.ManagedThreadId;
			DirectoryInfo dirInfo;
			dirInfo = !Directory.Exists (LogDirectory) ? Directory.CreateDirectory (LogDirectory) : new DirectoryInfo (LogDirectory);
			NamespaceLog = namespaceLog;
			TypeOfLog = typeOfLog;
			FileName = string.Format ("{0}-{1}-{2}T{3}.txt", NamespaceLog, TypeOfLog, DateTime.Now.ToLongTimeString ().Replace ('/', '-'), UniqueOriginatorThreadID);
			ulong IncreasementID = 0;
			FilePath = Path.Combine (dirInfo.FullName, FileName);
			while (File.Exists (FilePath)) {
				FileName = string.Format ("A{0}-{1}-{2}-{3}T{4}.txt", IncreasementID, NamespaceLog, TypeOfLog, DateTime.Now.ToLongTimeString ().Replace ('/', '-'), UniqueOriginatorThreadID);
				FilePath = Path.Combine (dirInfo.FullName, FileName);
				IncreasementID++;
			}
			IOToFile = new FileStream (FilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
			IOWriter = new StreamWriter (IOToFile);
			QueuesOfOutput.Enqueue (string.Format ("Started logging {0} namespace in {1} events at {2} on Thread {3}", NamespaceLog, TypeOfLog, DateTime.Now.ToLongTimeString (), UniqueOriginatorThreadID));
			IsActive = true;
			InternalThread = new Thread (new ThreadStart (InternalThreadProcess));
			InternalThread.Start ();
		}

		void InternalThreadProcess()
		{
			while (IsActive) {
				string message;
				bool attempt;
				do {
					attempt = QueuesOfOutput.TryDequeue (out message);
					if (!attempt)
						continue;
					IOWriter.WriteLine (message);
					IOWriter.Flush();
				} while (attempt);
				Thread.Sleep (1);
			}
			IOToFile.Close ();
		}

		public void LogException (Exception ex)
		{
			if (!IsActive)
				return;
				QueuesOfOutput.Enqueue (string.Format ("{0} UTC: Exception at {1}", DateTime.UtcNow.ToShortTimeString (), ex.Source));
				QueuesOfOutput.Enqueue (ex.Message);
				QueuesOfOutput.Enqueue ("");
		}

		public void LogEvent (string message)
		{
			if (!IsActive)
				return;
			QueuesOfOutput.Enqueue (string.Format ("{0}: {1}", DateTime.UtcNow.ToShortTimeString (), message));
			QueuesOfOutput.Enqueue ("");
		}

		public void Close()
		{
			IsActive = false;
		}

		#region IDisposable implementation

		public void Dispose ()
		{
			Close ();
		}

		#endregion

		// Allow the program to clear the logs.
		public static bool ClearLogDirectory()
		{
			if (!Directory.Exists (LogDirectory)) {
				return false;
			}

			try
			{
				var dirInfo = new DirectoryInfo(LogDirectory);
				var files = dirInfo.GetFiles();

				foreach (var file in files)
				{
					File.Delete(file.FullName);
				}
			}
			catch (Exception) {
				return false;
			}
			return true;
		}
	}
}

*/