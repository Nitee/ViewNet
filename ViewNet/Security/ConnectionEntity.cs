using System;

namespace ViewNet
{

	/// <summary>
	/// Identification and information for specific Connection
	/// </summary>
	public class ConnectionEntity : IDisposable
	{
		/// <summary>
		/// Identifier for local connection entity
		/// </summary>
		public ulong LocalID { get; set; }

		/// <summary>
		/// Identifier for remote connection entity
		/// </summary>
		public ulong RemoteID { get; set; }

		/// <summary>
		/// Name of the entity
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// A previous key of the last Diffie-Hellman Key Exchange for preserving the chain
		/// that the secure communication.
		/// </summary>
		public byte[] ServerKey { get; set; }

		public bool IsBlacklisted { get; set; }

		public ConnectionEntity ()
		{
			LocalID = ulong.MaxValue;
			RemoteID = ulong.MaxValue;
			Title = string.Empty;
			ServerKey = null;
			IsBlacklisted = false;
		}

		public ConnectionEntity (ulong localid, ulong remoteid, string title, byte[] previouskey, bool isBlacklisted)
		{
			LocalID = localid;
			RemoteID = remoteid;
			Title = title;
			ServerKey = previouskey;
			IsBlacklisted = isBlacklisted;
		}

		/// <summary>
		/// Dispose all resources and destruction of datas
		/// </summary>
		public void Close ()
		{
			LocalID = ulong.MaxValue;
			RemoteID = ulong.MaxValue;
			Title = string.Empty;
			Array.Clear (ServerKey, 0, ServerKey.Length);
		}

		#region IDisposable implementation

		public void Dispose ()
		{
			Close ();
		}

		#endregion
	}
}
