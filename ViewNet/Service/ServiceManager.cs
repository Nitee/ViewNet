using System;
using System.Threading;
using System.Collections.Generic;
using System.Reflection;
using System.Net.Sockets;
using System.Net;

namespace ViewNet
{
	public class ServiceManager
	{
		#region Working Objects
		volatile bool IsHost;

		volatile bool IsBeingDisconnected;

		Thread serviceThread { get; set; }

		ulong NewID;

		Dictionary<ulong, IService> ActiveServices{ get; set; }

		Dictionary<string, ulong> NameOfService { get; set; }

		Dictionary<string, Type> NameToAvailableService{ get; set; }

		ICryptoManager _manager;

		volatile bool PermitServiceAdd = true;

		ByteServiceStream _primaryStream { get; set; }

		System.Timers.Timer StopTimer {get;set;}

		PrimaryCache Maincache {get;set;}
		#endregion

		#region Public Properties
		public IPEndPoint RemoteEndPoint {get;set;}
		public bool IsBlacklisted { get; set; }
		public User RemoteUser { get; set; }
		public User LocalUser {get;set;}
		public ushort MaxRetries{get;set;}
		#endregion

		public ServiceManager (IPEndPoint ip, PrimaryCache cache, CryptoStandard crypto, ushort maxRetries)
		{
			Load ();
			NewID = 0; // Start off at the lower half of the ULong Available integer space for Client Side
			_manager = FindStandardCryptoManager (ip, crypto);
			Start ();
			RemoteEndPoint = ip;
			Maincache = cache;
			MaxRetries = maxRetries;
		}

		public ServiceManager (TcpClient tcpClient, PrimaryCache cache, CryptoStandard crypto, ushort maxRetries)
		{
			IsHost = true;
			Load ();
			NewID = (ulong)long.MaxValue + 1; // Start off at higher half of ULong Available Integer space for Server side
			_manager = FindStandardCryptoManager (tcpClient, crypto);
			Start ();
			RemoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
			Maincache = cache;
			MaxRetries = maxRetries;
		}

		/// <summary>
		/// Load the defaults
		/// </summary>
		void Load ()
		{
			_primaryStream = new ByteServiceStream (
				new [] {
					typeof(MAddService),
					typeof(MRemoveService),
					typeof(MServiceMismatch),
					typeof(MServiceRead),
					typeof(MGreet),
					typeof(MLogin),
					typeof(MRegister),
					typeof(RBlacklisted),
					typeof(RGreet),
					typeof(RLogin),
					typeof(RRegister)
				}
			);
			ActiveServices = new Dictionary<ulong, IService> ();
			NameToAvailableService = new Dictionary<string, Type> ();
			NameOfService = new Dictionary<string, ulong> ();
			LoadAvailableServices ();
			serviceThread = new Thread (ServiceThreadProcess);
			serviceThread.Name = "Service Manager Thread";
		}

		/// <summary>
		/// Load all the available services that are already built in current program
		/// Extensible services will be implemented later...
		/// </summary>
		void LoadAvailableServices ()
		{
			var thisAssembly = Assembly.GetAssembly (typeof(IService));
			var types = thisAssembly.GetTypes ();
			foreach (var indiv in types) {
				bool containsIService = false;
				var checkIt = indiv.GetInterfaces ();
				foreach (var check in checkIt) {
					if (check.FullName == typeof(IService).FullName) {
						containsIService = true;
						break;
					}
				}

				if (!containsIService)
					continue;

				NameToAvailableService.Add (indiv.FullName, indiv);
			}
		}

		/// <summary>
		/// Gets the active services.
		/// </summary>
		/// <returns>The active services.</returns>
		public IService[] GetActiveServices ()
		{
			IService[] services;
			lock (ActiveServices) {
				services = new IService[ActiveServices.Values.Count];
				ActiveServices.Values.CopyTo (services, 0);
			}

			return services;
		}

		static ICryptoManager FindStandardCryptoManager (IPEndPoint ip, CryptoStandard crypto)
		{
			switch (crypto) {
			case CryptoStandard.DHAES256:
				{
					return new DHToAES256Manager (ip);
				}

			case CryptoStandard.None:
				{
					// Seriously not recommended!
					return new NoCryptoManager (ip);
				}

			default:
				{
					return new DHToAES256Manager (ip);
				}
			}
		}

		static ICryptoManager FindStandardCryptoManager (TcpClient tcpClient, CryptoStandard crypto)
		{
			switch (crypto) {
			case CryptoStandard.None:
				{
					return new NoCryptoManager (tcpClient);
				}
			case CryptoStandard.DHAES256:
				{
					return new DHToAES256Manager (tcpClient);
				}
			default:
				{
					return new DHToAES256Manager (tcpClient);
				}
			}
		}

		/// <summary>
		/// Start the service manager
		/// </summary>
		public void Start ()
		{
			_manager.Start ();
			serviceThread.Start ();
		}

		/// <summary>
		/// Stop everything
		/// </summary>
		public void Stop ()
		{
			_manager.Stop ();
		}

		/// <summary>
		/// Allows the services to be added
		/// </summary>
		public void AllowServices ()
		{
			PermitServiceAdd = true;
		}

		/// <summary>
		/// Essentially tell the service manager not to accept any more services
		/// </summary>
		public void ForbidServices ()
		{
			PermitServiceAdd = false;
		}

		public ulong AddNewService (ref IService service)
		{
			if (StopIfNotRunningAnymore ())
				return 0;
			if (NameOfService.ContainsKey (service.GetType ().FullName)) {
				var id = NameOfService [service.GetType ().FullName];
				// set the reference
				service = ActiveServices [id];
				return id;
			}

			lock (ActiveServices) {
				ActiveServices.Add (NewID, service);
				service.IsActive = true;
				NameOfService.Add (service.GetType ().FullName, NewID);
			}

			var NewService = new MAddService (NewID, service.GetType ().FullName);
			_primaryStream.EnqueueMessage (NewService);

			NewID++;
			return NewID - 1;
		}
		// This will modify the references when needed.
		public ulong[] AddNewServices (ref IService[] services)
		{
			var ServicesID = new List<ulong> ();
			for (var I = 0; I < services.Length; I++) {
				ServicesID.Add (AddNewService (ref services [I]));
			}
			return ServicesID.ToArray ();
		}

		/// <summary>
		/// Remove a service and let the other side know to remove the service
		/// </summary>
		/// <param name="serviceID">Service I.</param>
		public void RemoveService (ulong serviceID)
		{
			if (StopIfNotRunningAnymore ())
				return;
			lock (ActiveServices) {
				if (ActiveServices.ContainsKey (serviceID)) {
					ActiveServices [serviceID].IsActive = false;
					NameOfService.Remove (ActiveServices [serviceID].GetType ().FullName);
					ActiveServices.Remove (serviceID);
					var RemoveAService = new MRemoveService (serviceID);
					_primaryStream.EnqueueMessage (RemoveAService);
				}
			}
		}

		/// <summary>
		/// Request a new key (Won't do anything if you're a client. Server have reserved right of this.)
		/// </summary>
		public void NewPublicKey ()
		{
			if (StopIfNotRunningAnymore ())
				return;
			_manager.NewPublicKey ();
		}

		/// <summary>
		/// Essentially what it does, it would stop this manager and every other managers if
		/// manager is no longer running
		/// </summary>
		bool StopIfNotRunningAnymore ()
		{
			if (!IsRunning ())
				Stop ();
			return !IsRunning ();
		}

		/// <summary>
		/// Determines if the manager is still running
		/// </summary>
		/// <returns><c>true</c> if this instance is running; otherwise, <c>false</c>.</returns>
		public bool IsRunning ()
		{
			return _manager.IsRunning;
		}

		/// <summary>
		/// Check the Message itself and assign the message to proper process calls.
		/// </summary>
		/// <param name="message">Message.</param>
		void Process (object message)
		{
			if (IsBeingDisconnected)
				Stop ();
			if (message.GetType () == typeof(MServiceRead))
				ProcessMServiceRead ((MServiceRead)message);
			if (message.GetType () == typeof(MAddService))
				ProcessMAddService ((MAddService)message);
			if (message.GetType () == typeof(MRemoveService))
				ProcessMRemoveService ((MRemoveService)message);
			if (message.GetType () == typeof(MServiceMismatch))
				ProcessMServiceMismatch ();
			if (message.GetType () == typeof(MGreet))
				ProcessMServiceMismatch ();
		}

		#region Inner Service Message Processing Functions
		// Essentially process any message that come to this service manager
		// It may not be strictly Service, but it include Login mechanism along with
		// other security mechanisms.

		/// <summary>
		/// Process an incoming stream of data for specific service
		/// </summary>
		/// <param name="data">Data.</param>
		void ProcessMServiceRead (MServiceRead data)
		{
			if (ActiveServices.ContainsKey (data.ID))
				ActiveServices [data.ID].Read (data.Data);
		}

		/// <summary>
		/// Processing a call to add a new service
		/// </summary>
		/// <param name="data">Data.</param>
		void ProcessMAddService (MAddService data)
		{
			// If already exists, then the whole protocol isn't working, so let's put a stop to that.
			if (ActiveServices.ContainsKey (data.ID)) { // Mismatch problem, maybe implement later.
				_primaryStream.EnqueueMessage (new MServiceMismatch ());
				Stop ();
			} else {
				// Add permission management
				if (!PermitServiceAdd) {
					// Pretty  much tell the other side to remove that service, because it's refused.
					_primaryStream.EnqueueMessage (new MRemoveService (data.ID));
				} else {
					var create = NameToAvailableService [data.FullName].GetConstructors ();
					// Automatically Initialize an Empty Service
					foreach (var init in create) {
						if (init.GetParameters ().Length == 0) {
							ActiveServices.Add (data.ID, (IService)init.Invoke (null));
							NameOfService.Add (ActiveServices [data.ID].GetType ().FullName, data.ID);
							ActiveServices [data.ID].IsActive = true;
							break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Processing a call to remove a service.
		/// </summary>
		/// <param name="data">Data.</param>
		void ProcessMRemoveService (MRemoveService data)
		{
			if (ActiveServices.ContainsKey (data.ID)) {
				NameOfService.Remove (ActiveServices [data.ID].GetType ().FullName);
				ActiveServices [data.ID].IsActive = false;
				ActiveServices.Remove (data.ID);
			}
		}

		/// <summary>
		/// Processing a mismatch call from other side, something seriously went wrong...
		/// </summary>
		void ProcessMServiceMismatch ()
		{
			// No longer can keep up the connection as it is corrupted.
			Stop ();
		}

		// TODO: Improve the security mechanism for connection identification and login.

		void ProcessMGreet(MGreet greeting)
		{
			if (!IsHost) {
				InitiateBlackListing ();
			}
			Assembly thisAssembly = typeof(ServiceManager).Assembly;
			Version currentVersion = thisAssembly.GetName ().Version;

			if (greeting.Version != currentVersion.ToString ()) {
				_primaryStream.EnqueueMessage (new RGreet (0, RGreet.ConnectionState.WrongVersion));
				InitateDisconnection ();
				return;
			}

			if (greeting.ID > 0) {
				if (!Maincache.ConnectionEntities.ContainsKey (greeting.ID)) {
					ushort retries = MaxRetries;

					if (Maincache.IPToRetries.ContainsKey (RemoteEndPoint.Address))
						retries = Maincache.IPToRetries [RemoteEndPoint.Address];
					_primaryStream.EnqueueMessage (new RGreet (retries, RGreet.ConnectionState.ConnectionAuthenicated));
				}
			} else {
				Maincache.ConnectionEntities.GetOrAdd (greeting.ID, new ConnectionEntity ());
			}
		}
		#endregion

		/// <summary>
		/// Internal Service Manager Thread Process
		/// This process for Recieving Packets, Processing the Packets, 
		/// </summary>
		void ServiceThreadProcess ()
		{
			while (_manager.IsRunning) {
				lock (ActiveServices) {
					if (_manager.AvailablePackets () > 0) {
						var packet = _manager.RetrievePacket ();
						if (packet.Length <= 0)
							continue;
						_primaryStream.Read (packet);
					}
					var theMessage = _primaryStream.AttemptDequeueMessage ();
					if (theMessage != null) {
						Process (theMessage);
					}
					var EnumerateServices = ActiveServices.GetEnumerator ();

					while (EnumerateServices.MoveNext ()) {
						if (EnumerateServices.Current.Value.Available ()) {
							byte[] content = EnumerateServices.Current.Value.Write ();
							if (content.Length > 65536) {
								continue; // Reject the message if exceeded the limit
							}
							_primaryStream.EnqueueMessage (
								new MServiceRead (EnumerateServices.Current.Key, content)
							);
						}
					}
					_manager.SendMessage (_primaryStream.Write (65536));
				}
				Thread.Sleep (0);
			}
		}

		/// <summary>
		/// Initiates the disconnection and black list timing.
		/// </summary>
		/// <param name="message">Message.</param>
		void InitiateBlackListing(string message)
		{
			if (IsBeingDisconnected)
				return;
			IsBeingDisconnected = true;
			_primaryStream.EnqueueMessage (new RBlacklisted (message));
			StopTimer = new System.Timers.Timer ();
			StopTimer.AutoReset = false;
			StopTimer.Elapsed += (sender, e) => Stop ();
			StopTimer.Interval = 10000; // Allow 10 seconds to process outstream
			StopTimer.Start ();
		}

		/// <summary>
		/// Initiates the disconnection and black list timing.
		/// </summary>
		void InitiateBlackListing()
		{
			if (IsBeingDisconnected)
				return;
			IsBeingDisconnected = true;
			_primaryStream.EnqueueMessage (new RBlacklisted ());
			StopTimer = new System.Timers.Timer ();
			StopTimer.AutoReset = false;
			StopTimer.Elapsed += (sender, e) => Stop ();
			StopTimer.Interval = 10000; // Allow 10 seconds to process outstream
			StopTimer.Start ();
		}

		/// <summary>
		/// Initates the disconnection timer.
		/// </summary>
		void InitateDisconnection()
		{
			if (IsBeingDisconnected)
				return;
			IsBeingDisconnected = true;
			StopTimer = new System.Timers.Timer ();
			StopTimer.AutoReset = false;
			StopTimer.Elapsed += (sender, e) => Stop ();
			StopTimer.Interval = 10000; // Allow 10 seconds to process outstream
			StopTimer.Start ();
		}
	}
}

