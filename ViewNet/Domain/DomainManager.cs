using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.IO;

namespace ViewNet
{
	/// <summary>
	/// A top level manager that manage multiple connections and servers to allow
	/// cross delegation communication between service managers while utilizing the
	/// fundamental security measures such as User/Group permission and roles
	/// </summary>
	public class DomainManager
	{
		const string CacheFileName = "ViewNet.cache";

		TcpListener TCPServer { get; set; }

		Dictionary<IPEndPoint, ServiceManager> ActiveViewNetManagers { get; set; }

		volatile bool _IsHosting;

		Thread serverThread { get; set; }

		const int StandardPort = 10030;

		const ushort RetriesMax = 3;

		byte[] PrivateKey { get; set; }

		const CryptoStandard DefaultStandard = CryptoStandard.DHAES256;
		ConcurrentQueue<IPEndPoint> DisconnectedClients = new ConcurrentQueue<IPEndPoint> ();
		PrimaryCache manageCache = new PrimaryCache ();
		ConcurrentDictionary<User, bool> UserToLock = new ConcurrentDictionary<User, bool> ();

		/// <summary>
		/// Gets a value indicating whether this instance is hosting.
		/// </summary>
		/// <value><c>true</c> if this instance is hosting; otherwise, <c>false</c>.</value>
		public bool IsHosting {
			get {
				return _IsHosting;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ViewNet.DomainManager"/> class.
		/// </summary>
		public DomainManager ()
		{
			InitializeObjects ();
		}

		/// <summary>
		/// Initializes the objects.
		/// </summary>
		void InitializeObjects ()
		{
			ActiveViewNetManagers = new Dictionary<IPEndPoint, ServiceManager> ();
		}

		/// <summary>
		/// Starts the hosting.
		/// </summary>
		/// <param name="ip">Ip.</param>
		public void StartHosting (IPAddress ip)
		{
			StartHosting (ip, StandardPort);
		}

		/// <summary>
		/// Loads the cache.
		/// </summary>
		/// <param name="key">Key.</param>
		public void LoadCache (string key)
		{
			if (!File.Exists (CacheFileName))
				return;
			var buffer = File.ReadAllBytes (CacheFileName);
			manageCache.Import (buffer, key);
			var enumerate = manageCache.Users.GetEnumerator ();
			while (enumerate.MoveNext ())
				UserToLock.TryAdd (enumerate.Current.Value, false);
		}

		/// <summary>
		/// Saves the cache.
		/// </summary>
		/// <param name="key">Key.</param>
		public void SaveCache (string key)
		{
			File.WriteAllBytes (CacheFileName, manageCache.Export (key));
		}

		/// <summary>
		/// Starts the hosting.
		/// </summary>
		/// <param name="ip">Ip.</param>
		/// <param name="port">Port.</param>
		public void StartHosting (IPAddress ip, int port)
		{
			if (_IsHosting)
				return;
			try {
				TCPServer = new TcpListener (ip, port);
				TCPServer.Start ();
				_IsHosting = true;
				serverThread = new Thread (new ThreadStart (ListenProcess));
				serverThread.Name = "Domain Hosting Thread";
				serverThread.Start ();
			} catch (Exception) {
				if (TCPServer != null) {
					TCPServer.Stop ();
					TCPServer = null;
				}
				if (serverThread != null) {
					serverThread.Join ();
					serverThread = null;
				}
				throw;
			}
		}

		/// <summary>
		/// Stops the hosting.
		/// </summary>
		public void StopHosting ()
		{
			if (!_IsHosting)
				return;
			_IsHosting = false;
			serverThread.Join ();
			serverThread = null;

			if (TCPServer != null)
				lock (TCPServer) {
					TCPServer.Stop ();
					TCPServer = null;
				}
			lock (ActiveViewNetManagers) {
				if (ActiveViewNetManagers.Count > 0) {
					foreach (var manager in ActiveViewNetManagers)
						manager.Value.Stop ();
					ActiveViewNetManagers.Clear ();
				}
			}
		}

		/// <summary>
		/// Connects to a host.
		/// </summary>
		/// <param name="ip">Ip.</param>
		/// <param name="standard">Standard.</param>
		public void ConnectToHost (IPAddress ip, CryptoStandard standard)
		{
			ConnectToHost (ip, StandardPort, standard);
		}

		/// <summary>
		/// Connects to a host.
		/// </summary>
		/// <param name="ip">Ip.</param>
		/// <param name="port">Port.</param>
		/// <param name="standard">Standard.</param>
		public void ConnectToHost (IPAddress ip, int port, CryptoStandard standard)
		{
			var endPoint = new IPEndPoint (ip, port);
			lock (ActiveViewNetManagers) {
				ActiveViewNetManagers.Add (endPoint, 
					new ServiceManager (
						endPoint, manageCache, standard, RetriesMax
					));
			}
		}

		/// <summary>
		/// Connects to a host.
		/// </summary>
		/// <param name="ip">Ip.</param>
		/// <param name="port">Port.</param>
		public void ConnectToHost (IPAddress ip, int port)
		{
			var endPoint = new IPEndPoint (ip, port);
			lock (ActiveViewNetManagers) {
				ActiveViewNetManagers.Add (endPoint, 
					new ServiceManager (
						endPoint, manageCache, CryptoStandard.DHAES256, RetriesMax
					));
			}
		}

		/// <summary>
		/// Connects to a host.
		/// </summary>
		/// <param name="ip">Ip.</param>
		public void ConnectToHost (IPAddress ip)
		{
			var endPoint = new IPEndPoint (ip, StandardPort);
			lock (ActiveViewNetManagers) {
				ActiveViewNetManagers.Add (endPoint, 
					new ServiceManager (
						endPoint, manageCache, CryptoStandard.DHAES256, RetriesMax
					));
			}
		}

		/// <summary>
		/// Gets a list of connected clients.
		/// </summary>
		/// <returns>The list of connected clients.</returns>
		public IPEndPoint[] GetListOfConnectedClients ()
		{
			var clients = new List<IPEndPoint> ();
			lock (ActiveViewNetManagers) {
				var enumerate = ActiveViewNetManagers.GetEnumerator ();
				while (enumerate.MoveNext ())
					clients.Add (enumerate.Current.Key);
			}
			return clients.ToArray ();
		}
			
		//TODO: Re-Do Service Management on Domain Level
		/*
		public bool AddDomainService (IPEndPoint ipend, IDomainService service)
		{
			throw new NotImplementedException ();
			var services = service.AddConnection (ipend);
			ActiveViewNetManagers [ipend].AddNewServices (ref services);
			return false;
		}
		*/
		/// <summary>
		/// Add a service to a node.
		/// </summary>
		/// <returns><c>true</c>, if service to node was added, <c>false</c> otherwise.</returns>
		/// <param name="ipend">Ipend.</param>
		/// <param name="service">Service.</param>
		public bool AddServiceToNode (IPEndPoint ipend, IService service)
		{
			lock (ActiveViewNetManagers) {
				if (!ActiveViewNetManagers.ContainsKey (ipend))
					return false;

				ActiveViewNetManagers [ipend].AddNewService (ref service);
			}
			return true;
		}

		void ListenProcess ()
		{
			while (_IsHosting) {
				// Check for any pending connection requests
				lock (TCPServer)
					if (TCPServer.Pending ()) {
						lock (ActiveViewNetManagers) {
							var newSocket = TCPServer.AcceptTcpClient ();
							ActiveViewNetManagers.Add ((IPEndPoint)newSocket.Client.RemoteEndPoint, 
								new ServiceManager (
									newSocket, 
									manageCache, 
									DefaultStandard,
									RetriesMax));
						}
					}
				// Check if any client has been disconnected
				lock (ActiveViewNetManagers) {
					var enumerate = ActiveViewNetManagers.GetEnumerator ();
					while (enumerate.MoveNext ()) {
						if (!enumerate.Current.Value.IsRunning ()) {
							DisconnectedClients.Enqueue (enumerate.Current.Key);
						}
					}
				}
				if (DisconnectedClients.Count > 0)
					lock (ActiveViewNetManagers) {
						while (true) {
							IPEndPoint currentDisconnection;
							var check = DisconnectedClients.TryDequeue (out currentDisconnection);
							if (!check)
								break;
							ActiveViewNetManagers.Remove (currentDisconnection);
						}
					}
				Thread.Sleep (1);
			}
		}

		/// <summary>
		/// Checks the state of connection.
		/// </summary>
		/// <returns><c>true</c>, if state of connection was checked, <c>false</c> otherwise.</returns>
		/// <param name="ipendpoint">Ipendpoint.</param>
		public bool CheckStateOfConnection (IPEndPoint ipendpoint)
		{
			bool result = false;
			lock (ActiveViewNetManagers)
				if (ActiveViewNetManagers.ContainsKey (ipendpoint))
					result = ActiveViewNetManagers [ipendpoint].IsRunning ();
			return result;
		}

		/// <summary>
		/// Checks the state of the host.
		/// </summary>
		/// <returns><c>true</c>, if host state was checked, <c>false</c> otherwise.</returns>
		public bool CheckHostState ()
		{
			return serverThread.ThreadState == ThreadState.Running;
		}

		/// <summary>
		/// Stop the entire domain connection/hosting.
		/// </summary>
		public void Stop ()
		{
			StopHosting ();
			lock (ActiveViewNetManagers) {
				var enumerate = ActiveViewNetManagers.GetEnumerator ();
				while (enumerate.MoveNext ()) {
					enumerate.Current.Value.Stop ();
				}
			}
		}
	}
}

