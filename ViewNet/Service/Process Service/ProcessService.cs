using System;
using ViewNet;
using System.Collections.Generic;
using System.Diagnostics;

namespace ViewNet
{
	/// <summary>
	/// Service template.
	/// </summary>
	public class ProcessService : IService
	{
		ByteServiceStream managedStream { get; set; }

		volatile bool _isActive;

		Dictionary<ulong, Process> IDToProcess { get; set; }

		Dictionary<ulong, bool> IDToRedirectionCheck { get; set; }

		public ProcessService ()
		{
			managedStream = new ByteServiceStream (new [] {
				typeof(ExecuteProcess),
				typeof(RemoveProcess),
				typeof(ProcessOutputFeed),
				typeof(ProcessWriteFeed),
				typeof(ProcessError),
				typeof(ProcessErrorFeed)
			});

			IDToProcess = new Dictionary<ulong, Process> ();
			IDToRedirectionCheck = new Dictionary<ulong, bool> ();
		}

		public byte[] Write ()
		{
			return managedStream.Write (65536);
		}

		public void Read (byte[] data)
		{
			managedStream.Read (data);
		}

		void Process ()
		{
			var message = managedStream.AttemptDequeueMessage ();
			if (message == null)
				return;
			if (message.GetType () == typeof(ExecuteProcess)) {
				ProcessExecuteProcess ((ExecuteProcess)message);
			}
		}

		void ProcessExecuteProcess (ExecuteProcess execute)
		{
			IDToProcess.Add (execute.ID, new Process ());
			IDToRedirectionCheck.Add (execute.ID, execute.RedirectStream);
			if (execute.RedirectStream) {
				IDToProcess [execute.ID].StartInfo.RedirectStandardError = true;
				IDToProcess [execute.ID].StartInfo.RedirectStandardInput = true;
				IDToProcess [execute.ID].StartInfo.RedirectStandardOutput = true;
				IDToProcess [execute.ID].StartInfo.UseShellExecute = true;
			}
			IDToProcess [execute.ID].StartInfo.FileName = execute.Path;
			try {
				IDToProcess [execute.ID].Start ();
			} catch (Exception ex) {
				managedStream.EnqueueMessage (new ProcessError (execute.ID, ex.Message));
				IDToProcess.Remove (execute.ID);
			}
		}

		public bool Available ()
		{
			return managedStream.Avaliable ();
		}

		public bool IsActive {
			get {
				return _isActive;
			}
			set {
				_isActive = value;
			}
		}
	}

	public class ExecuteProcess
	{
		public ExecuteProcess ()
		{

		}

		public ExecuteProcess (ulong id, string path, bool redirectStream)
		{
			ID = id;
			Path = path;
			RedirectStream = redirectStream;
		}

		public ulong ID { get; set; }

		public string Path { get; set; }

		public bool RedirectStream { get; set; }
	}

	public class RemoveProcess
	{
		public RemoveProcess ()
		{

		}

		public RemoveProcess (ulong id)
		{
			ID = id;
		}

		public ulong ID { get; set; }
	}

	public class ProcessOutputFeed
	{
		public ProcessOutputFeed ()
		{

		}

		public ProcessOutputFeed (ulong id, byte[] data)
		{
			ID = id;
			Data = data;
		}

		public ulong ID { get; set; }

		public byte[] Data { get; set; }
	}

	public class ProcessWriteFeed
	{
		public ProcessWriteFeed ()
		{

		}

		public ProcessWriteFeed (ulong id, byte[] data)
		{
			ID = id;
			Data = data;
		}

		public ulong ID { get; set; }

		public byte[] Data { get; set; }
	}

	public class ProcessError
	{
		public ProcessError (ulong id, string message)
		{
			ID = id;
			Message = message;
		}

		public ulong ID { get; set; }

		public string Message { get; set; }
	}

	public class ProcessErrorFeed
	{
		public ProcessErrorFeed (ulong id, byte[] data)
		{
			ID = id;
			Data = data;
		}

		public ulong ID { get; set; }

		public byte[] Data { get; set; }
	}
}