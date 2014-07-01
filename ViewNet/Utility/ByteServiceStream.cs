using System;
using System.IO;
using System.Collections.Generic;
using Salar.Bois;
using System.Reflection;

namespace ViewNet
{
	/// <summary>
	/// A reliable stream that manage Input/Output streams and simplify the process
	/// for services that is limited to amount of data it can be exchanged with remote services
	/// </summary>
	public class ByteServiceStream
	{
		Dictionary<byte, Type> IDToType = new Dictionary<byte, Type> ();
		Dictionary<Type, byte> TypeToID = new Dictionary<Type, byte> ();
		BoisSerializer Serializer = new BoisSerializer ();
		MemoryStream InStream = new MemoryStream ();
		MemoryStream OutStream = new MemoryStream ();
		MethodInfo DefaultDeserializationMethod;

		/// <summary>
		/// Initializes a new instance of the <see cref="ViewNet.ByteServiceStream"/> class.
		/// </summary>
		/// <param name = "messageObjects"></param>
		public ByteServiceStream (params Type[] messageObjects)
		{
			byte idRoll = 0;
			foreach (var obj in messageObjects) {
				IDToType.Add (idRoll, obj);
				TypeToID.Add (obj, idRoll);
				idRoll++;
			}

			DefaultDeserializationMethod = Serializer.GetType ().GetMethods () [4];
		}

		/// <summary>
		/// Enqueues the message.
		/// </summary>
		/// <param name='data'>
		/// This will serialize the Data object to stream
		/// </param>
		public void EnqueueMessage (object data)
		{
			var buffer = new MemoryStream ();
			// Check The Type of Enqueued Object
			byte TypeID = TypeToID [data.GetType ()];
			// Write the ID to the stream
			DataUtility.WriteAByteToStream (TypeID, buffer);
			// Inner Memory Stream
			var innerBuffer = new MemoryStream ();
			// Serialize the data into stream
			Serializer.Serialize (data, innerBuffer);
			// Write that Stream
			DataUtility.WriteBytesToStream (innerBuffer.ToArray (), buffer);
			// Lock the OutStream and enqueue the stream into output
			lock (OutStream) {
				OutStream.Seek (0, SeekOrigin.End);
				DataUtility.WriteOnlyBytesToStream (OutStream, buffer.ToArray ());
			}
		}

		byte _attemptedTypeID;
		int _attemptedLength;
		bool _attempted;

		public object AttemptDequeueMessage ()
		{
			lock (InStream) {
				if (_attempted) {
					if (InStream.Length < _attemptedLength)
						return null;
				} else {
					if (InStream.Length < 5)
						return null;
					InStream.Seek (0, SeekOrigin.Begin);
					_attemptedTypeID = DataUtility.ReadAByteFromStream (InStream);
					_attemptedLength = DataUtility.ReadInt32FromStream (InStream);
					_attempted = true;
					if (InStream.Length < _attemptedLength) {
						return null;
					}
				}
				InStream.Seek (5, SeekOrigin.Begin);
				var buffer = new byte[_attemptedLength];
				InStream.Read (buffer, 0, buffer.Length);
				ClearAndCopyMemoryStream (ref InStream, buffer.Length + 5);
				var GenericArgument = IDToType [_attemptedTypeID];
				var genericMethodInfo = DefaultDeserializationMethod.MakeGenericMethod (GenericArgument);
				var deserializedResult = genericMethodInfo.Invoke (Serializer,
					                         new object[] {
						buffer,
						0,
						buffer.Length
					});
				_attempted = false;
				return deserializedResult;
			}
		}

		public bool Avaliable ()
		{
			lock (OutStream)
				return OutStream.Length > 0;
		}

		public byte[] Write (int size)
		{
			var data = new byte[size];
			lock (OutStream) {
				OutStream.Seek (0, SeekOrigin.Begin);
				var fixLength = OutStream.Read (data, 0, data.Length);
				Array.Resize (ref data, fixLength);

				ClearAndCopyMemoryStream (ref OutStream, data.Length);
				return data;
			}
		}

		public void Read (byte[] data)
		{
			lock (InStream) {
				InStream.Seek (0, SeekOrigin.End);
				InStream.Write (data, 0, data.Length);
			}
		}

		static void ClearAndCopyMemoryStream (ref MemoryStream memStream, int length)
		{
			var BufferedCopy = new byte[memStream.Length - length];
			if (BufferedCopy.Length == 0) {
				memStream.Dispose ();
				memStream = new MemoryStream ();
				return;
			}

			Array.ConstrainedCopy (memStream.ToArray (), length, BufferedCopy, 0, BufferedCopy.Length);
			memStream.Dispose ();
			memStream = new MemoryStream ();
			memStream.Write (BufferedCopy, 0, BufferedCopy.Length);
		}
	}
}

