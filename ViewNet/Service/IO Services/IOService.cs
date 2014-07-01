using System;
using ViewNet;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ViewNet
{
	public class IOService : IService
	{
		volatile bool _IsActive;
		long UploadFeedID;
		long DownloadFeedID;
		public volatile bool SafeToOperate;
		Dictionary<long, FileFeed> UploadFromRemote = new Dictionary<long, FileFeed> ();
		Dictionary<long, FileFeed> UploadToRemote = new Dictionary<long, FileFeed> ();
		Dictionary<long, FileFeed> DownloadFromRemote = new Dictionary<long, FileFeed> ();
		Dictionary<long, FileFeed> DownloadToRemote = new Dictionary<long, FileFeed> ();
		readonly ByteServiceStream managedStream;

		Thread writeThread { get; set; }

		public string CurrentDirectory { get; set; }

		public string[] Directories { get; set; }

		public string[] Files { get; set; }

		string RemoteCurrentDirectory { get; set; }

		Dictionary<long, FileFeedFlag> UploadIDToFlag = new Dictionary<long, FileFeedFlag> ();
		Dictionary<long, FileFeedFlag> DownloadIDToFlag = new Dictionary<long, FileFeedFlag> ();

		public FileFeedFlag CheckDownloadState (long id)
		{
			FileFeedFlag flag;
			lock (DownloadIDToFlag) {
				if (DownloadIDToFlag.ContainsKey (id))
					flag = DownloadIDToFlag [id];
				else
					return FileFeedFlag.Unknown;
			}
			return flag;
		}

		public FileFeedFlag CheckUploadState (long id)
		{
			FileFeedFlag flag;
			lock (UploadIDToFlag) {
				if (UploadIDToFlag.ContainsKey (id))
					flag = UploadIDToFlag [id];
				else
					return FileFeedFlag.Unknown;
			}
			return flag;
		}

		public IOService ()
		{
			managedStream = new ByteServiceStream (new [] {
				typeof(FileUploadRequest),
				typeof(FileUploadResponse),
				typeof(FileDownloadRequest),
				typeof(FileDownloadResponse),
				typeof(BrowseRequest),
				typeof(BrowseResponse),
				typeof(UploadCancel),
				typeof(DownloadCancel),
				typeof(UploadHold),
				typeof(DownloadHold),
				typeof(DownloadComplete),
				typeof(UploadComplete),
				typeof(DownloadFeed),
				typeof(UploadFeed)
			});
		}

		public byte[] Write ()
		{
			return managedStream.Write (65536);
		}

		public void Read (byte[] data)
		{
			managedStream.Read (data);
			Process ();
		}

		protected void Process ()
		{
			while (true) {
				var item = managedStream.AttemptDequeueMessage ();
				if (item == null)
					return;
				var itemType = item.GetType ();
				// Handle the Upload Request
				if (itemType == typeof(FileUploadRequest))
					ProcessUploadRequest ((FileUploadRequest)item);

				// Handle The Upload Response
				if (itemType == typeof(FileUploadResponse))
					ProcessUploadResponse ((FileUploadResponse)item);

				// Handle the Download Request
				if (itemType == typeof(FileDownloadRequest))
					ProcessDownloadRequest ((FileDownloadRequest)item);

				// Handle The Download Response
				if (itemType == typeof(FileDownloadResponse))
					ProcessDownloadResponse ((FileDownloadResponse)item);

				// Handle the Browse Request
				if (itemType == typeof(BrowseRequest))
					ProcessBrowseRequest ((BrowseRequest)item);

				// Handle the Browse Response
				if (itemType == typeof(BrowseResponse))
					ProcessBrowseResponse ((BrowseResponse)item);

				// Handle the Download Feed
				if (itemType == typeof(DownloadFeed))
					ProcessDownloadFeed ((DownloadFeed)item);

				// Handle the Download Feed
				if (itemType == typeof(UploadFeed))
					ProcessUploadFeed ((UploadFeed)item);

				// Handle the Upload Complete
				if (itemType == typeof(UploadComplete))
					ProcessUploadComplete ((UploadComplete)item);

				// Handle the Download Feed
				if (itemType == typeof(DownloadComplete))
					ProcessDownloadComplete ((DownloadComplete)item);
			}
		}

		protected void ProcessUploadRequest (FileUploadRequest request)
		{
			Console.WriteLine ("Processing Request");
			// Combine the Request Path with Current Directory
			// Assign the ID given in Request to a File Feed Handler Object.
			// Set the File Feed as NotApproved to await calls
			// Enqueue the Message
			var selectedPath = Path.Combine (RemoteCurrentDirectory, request.FileName);

			// Permission check here and send a not allowed resposne if failed such check.

			lock (UploadFromRemote)
				UploadFromRemote.Add (request.FileUploadID, new FileFeed (request.FileSize, selectedPath, FileFeedFlag.Allowed, new FileStream (selectedPath, FileMode.Create)));
			var response = new FileUploadResponse ();
			response.ID = request.FileUploadID;
			response.Allowed = true;

			managedStream.EnqueueMessage (response);
		}

		protected void ProcessUploadResponse (FileUploadResponse response)
		{
			Console.WriteLine ("Processing Response");
			if (!UploadToRemote.ContainsKey (response.ID)) {
				var closeUpload = new UploadCancel ();
				closeUpload.ID = response.ID;
				managedStream.EnqueueMessage (closeUpload);
				return;
			}

			if (!response.Allowed) {
				if (UploadToRemote.ContainsKey (response.ID)) {
					UploadToRemote [response.ID].Feed.Dispose ();
					UploadToRemote.Remove (response.ID);
				}
				return;
			}

			UploadToRemote [response.ID].State = FileFeedFlag.Allowed;
			lock (UploadIDToFlag)
				if (UploadIDToFlag.ContainsKey (response.ID))
					UploadIDToFlag [response.ID] = FileFeedFlag.Allowed;
		}

		protected void ProcessDownloadRequest (FileDownloadRequest request)
		{
			var fullPath = Path.Combine (RemoteCurrentDirectory, request.FileName);

			// Permission Check

			if (!File.Exists (fullPath)) {
				var reply = new FileDownloadResponse ();
				reply.ID = request.ID;
				reply.Allowed = false;
				managedStream.EnqueueMessage (reply);
				return;
			}
			lock (DownloadToRemote) {
				var info = new FileInfo (fullPath);
				DownloadToRemote.Add (request.ID, new FileFeed (info.Length, info.Name, FileFeedFlag.Allowed, new FileStream (fullPath, FileMode.Open)));
				var response = new FileDownloadResponse ();
				response.ID = response.ID;
				response.Allowed = true;
				managedStream.EnqueueMessage (response);
			}
		}

		protected void ProcessDownloadResponse (FileDownloadResponse response)
		{
			lock (DownloadFromRemote) {
				if (!DownloadFromRemote.ContainsKey (response.ID)) {
					var cancel = new DownloadCancel ();
					cancel.ID = response.ID;
					managedStream.EnqueueMessage (cancel);
					return;
				}

				DownloadFromRemote [response.ID].State = FileFeedFlag.Allowed;
				lock (DownloadIDToFlag)
					if (DownloadIDToFlag.ContainsKey (response.ID))
						DownloadIDToFlag [response.ID] = FileFeedFlag.Allowed;
			}
		}

		protected void ProcessBrowseRequest (BrowseRequest request)
		{
			// Permission Check
			if (!Directory.Exists (request.Path)) {
				var nullresponse = new BrowseResponse ();
				managedStream.EnqueueMessage (nullresponse);
				return;
			}
			RemoteCurrentDirectory = new DirectoryInfo (request.Path).FullName;
			var response = new BrowseResponse ();
			var dirs = new List<string> ();
			var files = new List<string> ();
			var currentdir = new DirectoryInfo (request.Path);
			foreach (var item in currentdir.GetDirectories ())
				dirs.Add (item.Name);
			response.Dirs = dirs.ToArray ();

			foreach (var item in currentdir.GetFiles ())
				files.Add (item.Name);
			response.Files = files.ToArray ();
			response.DirPath = request.Path;
			managedStream.EnqueueMessage (response);
			Console.WriteLine ("Processed Browse Request");
		}

		protected void ProcessBrowseResponse (BrowseResponse response)
		{
			Console.WriteLine ("Processing Browse Response");
			// Doesn't really require anything to process, just simply apply
			if (CurrentDirectory != null)
				lock (CurrentDirectory)
					CurrentDirectory = response.DirPath;
			else
				CurrentDirectory = response.DirPath;

			if (Directories != null)
				lock (Directories)
					Directories = response.Dirs;
			else
				Directories = response.Dirs;

			if (Files != null)
				lock (Files)
					Files = response.Files;
			else
				Files = response.Files;

			SafeToOperate = true;
		}

		protected void ProcessDownloadComplete (DownloadComplete completed)
		{
			lock (DownloadFromRemote) {
				lock (DownloadIDToFlag)
					if (DownloadIDToFlag.ContainsKey (completed.ID))
						DownloadIDToFlag [completed.ID] = FileFeedFlag.Completed;
				if (DownloadFromRemote.ContainsKey (completed.ID)) {
					DownloadFromRemote [completed.ID].Feed.Close ();
					DownloadFromRemote.Remove (completed.ID);
				}
			}
		}

		protected void ProcessUploadComplete (UploadComplete completed)
		{
			lock (UploadFromRemote) {
				lock (UploadIDToFlag)
					if (UploadIDToFlag.ContainsKey (completed.ID))
						UploadIDToFlag [completed.ID] = FileFeedFlag.Completed;
				if (UploadFromRemote.ContainsKey (completed.ID)) {
					UploadFromRemote [completed.ID].Feed.Close ();
					UploadFromRemote.Remove (completed.ID);
				}
			}
		}

		protected void ProcessDownloadFeed (DownloadFeed feed)
		{
			lock (DownloadFromRemote) {
				if (!DownloadFromRemote.ContainsKey (feed.ID)) {
					return; // Error it in the future
				}

				DownloadFromRemote [feed.ID].Feed.Write (feed.Data, 0, feed.Data.Length);
			}
		}

		protected void ProcessUploadFeed (UploadFeed feed)
		{
			lock (UploadFromRemote) {
				if (!UploadFromRemote.ContainsKey (feed.ID)) {
					return; // Error it in the future
				}

				UploadFromRemote [feed.ID].Feed.Write (feed.Data, 0, feed.Data.Length);
			}
		}

		protected void ProcessUploadHold (UploadHold hold)
		{
			lock (UploadToRemote) {
				if (UploadToRemote.ContainsKey (hold.ID))
					UploadToRemote [hold.ID].State = FileFeedFlag.Hold;
			}

			lock (UploadIDToFlag) {
				if (UploadIDToFlag.ContainsKey (hold.ID))
					UploadIDToFlag [hold.ID] = FileFeedFlag.Hold;
			}
		}

		protected void ProcessDownloadHold (DownloadHold hold)
		{
			lock (DownloadFromRemote) {
				if (DownloadFromRemote.ContainsKey (hold.ID))
					DownloadFromRemote [hold.ID].State = FileFeedFlag.Hold;
			}

			lock (DownloadIDToFlag) {
				if (DownloadIDToFlag.ContainsKey (hold.ID))
					DownloadIDToFlag [hold.ID] = FileFeedFlag.Hold;
			}
		}

		protected void ProcessUploadContinue (UploadHold hold)
		{
			lock (UploadToRemote) {
				if (UploadToRemote.ContainsKey (hold.ID))
					UploadToRemote [hold.ID].State = FileFeedFlag.Allowed;
			}

			lock (UploadIDToFlag) {
				if (UploadIDToFlag.ContainsKey (hold.ID))
					UploadIDToFlag [hold.ID] = FileFeedFlag.Allowed;
			}
		}

		protected void ProcessDownloadContinue (DownloadHold hold)
		{
			lock (DownloadFromRemote) {
				if (DownloadFromRemote.ContainsKey (hold.ID))
					DownloadFromRemote [hold.ID].State = FileFeedFlag.Allowed;
			}

			lock (DownloadIDToFlag) {
				if (DownloadIDToFlag.ContainsKey (hold.ID))
					DownloadIDToFlag [hold.ID] = FileFeedFlag.Allowed;
			}
		}

		public bool Available ()
		{
			var availableOrNot = managedStream.Avaliable ();
			if (!availableOrNot)
				WriteProcess ();
			return managedStream.Avaliable ();
		}

		void StartWriteThread ()
		{
			if (writeThread != null) {
				if (writeThread.ThreadState != ThreadState.Running)
					writeThread = null;
				else
					return;
			}
			writeThread = new Thread (WriteProcess);
			writeThread.Start ();
		}

		void WriteProcess ()
		{
			byte[] buffer;
			int TotalAppend = 0;
			int OldAppend = 0;
			int tick = 0;
			const int maxTick = 10;
			var ListOfUploads = new List<long> ();
			var ListOfDownloads = new List<long> ();
			while (true) {
				lock (UploadToRemote) {
					var enumerate = UploadToRemote.GetEnumerator ();
					while (enumerate.MoveNext ()) {
						if (enumerate.Current.Value.State != FileFeedFlag.Allowed ||
						    ListOfUploads.Contains (enumerate.Current.Key)) {
							continue;
						}
						tick++;
						if (tick >= maxTick)
							break;
						buffer = new byte[8192];
						var reSize = enumerate.Current.Value.Feed.Read (buffer, 0, buffer.Length);
						if (reSize == 0) {
							ListOfUploads.Add (enumerate.Current.Key);
							continue;
						}
						TotalAppend += reSize;
						Array.Resize (ref buffer, reSize);
						var feed = new UploadFeed ();
						feed.ID = enumerate.Current.Key;
						feed.Data = (byte[])buffer.Clone ();

						managedStream.EnqueueMessage (feed);
					}
				}

				lock (DownloadToRemote) {
					var enumerate = DownloadToRemote.GetEnumerator ();
					while (enumerate.MoveNext ()) {
						if (enumerate.Current.Value.State != FileFeedFlag.Allowed ||
						    ListOfDownloads.Contains (enumerate.Current.Key)) {
							continue;
						}
						tick++;
						if (tick >= maxTick)
							break;
						buffer = new byte[8192];
						var reSize = enumerate.Current.Value.Feed.Read (buffer, 0, buffer.Length);
						if (reSize == 0) {
							ListOfDownloads.Add (enumerate.Current.Key);
							continue;
						}
						TotalAppend += reSize;
						Array.Resize (ref buffer, reSize);
						var feed = new DownloadFeed ();
						feed.ID = enumerate.Current.Key;
						feed.Data = (byte[])buffer.Clone ();

						managedStream.EnqueueMessage (feed);
					}
				}

				foreach (var id in ListOfUploads) {
					lock (UploadToRemote) {
						UploadToRemote.Remove (id);
						var completed = new UploadComplete ();
						completed.ID = id;
						managedStream.EnqueueMessage (completed);
						lock (UploadIDToFlag)
							if (UploadIDToFlag.ContainsKey (id)) {
								UploadIDToFlag [id] = FileFeedFlag.Completed;
							}
					}
				}

				foreach (var id in ListOfDownloads) {
					lock (DownloadToRemote) {
						DownloadToRemote.Remove (id);
						var completed = new DownloadComplete ();
						completed.ID = id;
						managedStream.EnqueueMessage (completed);
						lock (DownloadIDToFlag)
							if (DownloadIDToFlag.ContainsKey (id)) {
								DownloadIDToFlag [id] = FileFeedFlag.Completed;
							}
					}
				}

				if (TotalAppend > 131072 ||
				    TotalAppend == OldAppend) {
					break;
				}
				OldAppend = TotalAppend;
				tick = 0;
			}
		}

		public bool IsActive {
			get {
				return _IsActive;
			}
			set {
				_IsActive = value;
			}
		}

		public long UploadFile (string filepath)
		{
			if (!SafeToOperate)
				throw new Exception ("Need to await a Browse Response to process otherwise remote computer will outright reject your upload no matter what.");
			if (!File.Exists (filepath)) {
				throw new IOException ("The file you selected does not exists!");
			}
			var info = new FileInfo (filepath);

			var request = new FileUploadRequest ();
			request.FileName = info.Name;
			request.FileSize = info.Length;
			request.FileUploadID = UploadFeedID;

			// Prep the upload
			lock (UploadToRemote)
				UploadToRemote.Add (UploadFeedID, new FileFeed (info.Length, info.Name, FileFeedFlag.NotApproved, new FileStream (filepath, FileMode.Open)));
			lock (UploadIDToFlag)
				UploadIDToFlag.Add (UploadFeedID, FileFeedFlag.NotApproved);
			managedStream.EnqueueMessage (request);
			Console.WriteLine ("Sent Upload Request");
			UploadFeedID++;
			return UploadFeedID - 1;
		}

		public long DownloadFile (string file, string destination)
		{
			var request = new FileDownloadRequest ();
			request.FileName = file;
			request.ID = DownloadFeedID;

			lock (DownloadFromRemote)
				DownloadFromRemote.Add (DownloadFeedID, new FileFeed (-1, file, FileFeedFlag.Allowed, new FileStream (destination, FileMode.Create)));
			lock (DownloadIDToFlag)
				DownloadIDToFlag.Add (DownloadFeedID, FileFeedFlag.NotApproved);
			managedStream.EnqueueMessage (request);

			DownloadFeedID++;

			return DownloadFeedID - 1;
		}

		public void BrowsePath (string path)
		{
			var request = new BrowseRequest ();
			request.Path = path;
			managedStream.EnqueueMessage (request);
			Console.WriteLine ("Sending Browse Request");
		}

		public void HoldDownload (long id)
		{
			var holdDownload = new DownloadHold ();
			holdDownload.ID = id;
			managedStream.EnqueueMessage (holdDownload);
		}
	}


	public class FileUploadRequest
	{
		public string FileName { get; set; }

		public long FileSize { get; set; }

		public long FileUploadID { get; set; }

		public FileUploadRequest()
		{
			FileName = string.Empty;
		}

		public FileUploadRequest (string name, long size, long id)
		{
			FileName = name;
			FileSize = size;
			FileUploadID = id;
		}
	}

	public class FileDownloadRequest
	{
		public long ID { get; set; }

		public string FileName { get; set; }
	}

	public class FileDownloadResponse
	{
		public long ID { get; set; }

		public bool Allowed { get; set; }
	}

	public class FileUploadResponse
	{
		public long ID { get; set; }

		public bool Allowed { get; set; }
	}

	public class UploadFeed
	{
		public long ID { get; set; }

		public byte[] Data { get; set; }
	}

	public class DownloadFeed
	{
		public long ID { get; set; }

		public byte[] Data { get; set; }
	}

	public class BrowseRequest
	{
		public string Path { get; set; }
	}

	public class BrowseResponse
	{
		public string[] Dirs { get; set; }

		public string[] Files { get; set; }

		public string DirPath { get; set; }
	}

	public class UploadCancel
	{
		public long ID { get; set; }
	}

	public class DownloadCancel
	{
		public long ID { get; set; }
	}

	public class DownloadComplete
	{
		public long ID { get; set; }
	}

	public class UploadComplete
	{
		public long ID { get; set; }
	}

	public class UploadHold
	{
		public long ID { get; set; }
	}

	public class DownloadHold
	{
		public long ID { get; set; }
	}

	public class FileFeed
	{
		public long Size { get; set; }

		public string Name { get; set; }

		public FileFeedFlag State { get; set; }

		public FileStream Feed { get; set; }

		public FileFeed ()
		{

		}

		public FileFeed (long size, string name, FileFeedFlag state, FileStream stream)
		{
			Size = size;
			Name = name;
			State = state;
			Feed = stream;
		}
	}

	public enum FileFeedFlag
	{
		Allowed,
		NotApproved,
		Hold,
		Completed,
		Unknown
	}
}

