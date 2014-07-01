using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Salar.Bois;
namespace ViewNet
{
	/// <summary>
	/// Information for this ViewNet Client/Server
	/// </summary>
	public class PrimaryCache
	{
		// Don't judge me :(
		const int IVSize = 16;

		byte[] TempKey { get; set; }

		public ulong LocalUserIDCounter { get; set; }

		public ulong LocalConnEntityIDCounter { get; set; }

		public ulong LocalGroupIDCounter { get; set; }

		public ConcurrentDictionary<ulong, ConnectionEntity> ConnectionEntities { get; set; }

		public ConcurrentDictionary<ulong, User> Users { get; set; }

		public ConcurrentDictionary<ulong, Group> Groups { get; set; }

		public ConcurrentDictionary<IPAddress, ushort> IPToRetries { get; set; }

		readonly BoisSerializer serializer;

		public PrimaryCache ()
		{
			ConnectionEntities = new  ConcurrentDictionary<ulong,ConnectionEntity> ();
			Users = new  ConcurrentDictionary<ulong,User> ();
			Groups = new  ConcurrentDictionary<ulong,Group> ();
			IPToRetries = new ConcurrentDictionary<IPAddress, ushort> ();
			serializer = new BoisSerializer ();
			serializer.Initialize (new [] {
				typeof(ConcurrentDictionary<ulong,ConnectionEntity>),
				typeof(ConcurrentDictionary<ulong,User>),
				typeof(ConcurrentDictionary<ulong,Group>),
				typeof(ConcurrentDictionary<IPAddress, ushort>)
			});
			LocalUserIDCounter = 1;
			LocalConnEntityIDCounter = 1;
			LocalGroupIDCounter = 1;
		}

		public byte[] Export (string password)
		{
			TempKey = GenerateHash (password);
			var encryptor = Rijndael.Create ();
			encryptor.Key = TempKey;
			using (var ExportStream = new MemoryStream ()) {
				var TempStream = new MemoryStream ();
				encryptor.GenerateIV ();
				// Write plain IV in 16 bytes segment
				DataUtility.WriteOnlyBytesToStream (ExportStream, encryptor.IV);
				// Write Local Counter
				DataUtility.WriteUInt64ToStream (LocalUserIDCounter, ExportStream);
				DataUtility.WriteUInt64ToStream (LocalGroupIDCounter, ExportStream);
				DataUtility.WriteUInt64ToStream (LocalConnEntityIDCounter, ExportStream);
				// Write Connection Entities
				serializer.Serialize (ConnectionEntities, TempStream);
				var tempBuffer = TempStream.ToArray ();
				tempBuffer = encryptor.CreateEncryptor ().TransformFinalBlock (tempBuffer, 0, tempBuffer.Length);
				DataUtility.WriteBytesToStream (tempBuffer, ExportStream);
				TempStream.Close ();
				TempStream = new MemoryStream ();
				// Write Users
				serializer.Serialize (Users, TempStream);
				tempBuffer = TempStream.ToArray ();
				tempBuffer = encryptor.CreateEncryptor ().TransformFinalBlock (tempBuffer, 0, tempBuffer.Length);
				DataUtility.WriteBytesToStream (tempBuffer, ExportStream);
				TempStream.Close ();
				TempStream = new MemoryStream ();
				// Write Groups
				serializer.Serialize (Groups, TempStream);
				tempBuffer = TempStream.ToArray ();
				tempBuffer = encryptor.CreateEncryptor ().TransformFinalBlock (tempBuffer, 0, tempBuffer.Length);
				DataUtility.WriteBytesToStream (tempBuffer, ExportStream);
				TempStream.Close ();
				TempStream = new MemoryStream ();
				// Write IPToRetries
				serializer.Serialize (IPToRetries, TempStream);
				tempBuffer = TempStream.ToArray ();
				tempBuffer = encryptor.CreateEncryptor ().TransformFinalBlock (tempBuffer, 0, tempBuffer.Length);
				DataUtility.WriteBytesToStream (tempBuffer, ExportStream);
				TempStream.Close ();
				return ExportStream.ToArray ();
			}
		}

		public void Import (byte[] input, string key)
		{
			TempKey = GenerateHash (key);
			var inputStream = new MemoryStream (input);
			var IV = DataUtility.ReadOnlyBytesFromStream (inputStream, 16);
			var decryptor = Rijndael.Create ();
			decryptor.Key = TempKey;
			decryptor.IV = IV;
			LocalUserIDCounter = DataUtility.ReadUInt64FromStream (inputStream);
			LocalGroupIDCounter = DataUtility.ReadUInt64FromStream (inputStream);
			LocalConnEntityIDCounter = DataUtility.ReadUInt64FromStream (inputStream);
			var connectionEntitiesBuffer = DataUtility.ReadBytesFromStream (inputStream);
			connectionEntitiesBuffer = decryptor.CreateDecryptor ().TransformFinalBlock (connectionEntitiesBuffer, 0, connectionEntitiesBuffer.Length);
			var usersBuffer = DataUtility.ReadBytesFromStream (inputStream);
			usersBuffer = decryptor.CreateDecryptor ().TransformFinalBlock (usersBuffer, 0, usersBuffer.Length);
			var groupsBuffer = DataUtility.ReadBytesFromStream (inputStream);
			groupsBuffer = decryptor.CreateDecryptor ().TransformFinalBlock (groupsBuffer, 0, usersBuffer.Length);
			var ip2retriesBuffer = DataUtility.ReadBytesFromStream (inputStream);
			ip2retriesBuffer = decryptor.CreateDecryptor ().TransformFinalBlock (ip2retriesBuffer, 0, ip2retriesBuffer.Length);
			inputStream.Close ();
			ConnectionEntities = serializer.Deserialize<ConcurrentDictionary<ulong, ConnectionEntity>> (connectionEntitiesBuffer, 0, connectionEntitiesBuffer.Length);
			Users = serializer.Deserialize<ConcurrentDictionary<ulong, User>> (usersBuffer, 0, usersBuffer.Length);
			Groups = serializer.Deserialize<ConcurrentDictionary<ulong, Group>> (groupsBuffer, 0, groupsBuffer.Length);
			IPToRetries = serializer.Deserialize<ConcurrentDictionary<IPAddress, ushort>> (ip2retriesBuffer, 0, ip2retriesBuffer.Length);
		}
		// Generate Hash For Password
		static byte[] GenerateHash (string password)
		{
			using (SHA256 sha = SHA256.Create ())
				return sha.ComputeHash (Encoding.UTF8.GetBytes (password));
		}

		/// <summary>
		/// Dispose all the resources used by this chunk
		/// </summary>
		public void Close ()
		{
			ConnectionEntities.Clear ();
			Users.Clear ();
			Groups.Clear ();
			serializer.ClearCache ();
		}
	}
}
