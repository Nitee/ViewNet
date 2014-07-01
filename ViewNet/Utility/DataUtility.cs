using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace ViewNet
{
	/// <summary>
	/// Utility to keep the code clean and easy to understand
	/// </summary>
	static class DataUtility
	{
		/// <summary>
		/// Serializes the object to stream.
		/// </summary>
		/// <param name="data">Data.</param>
		/// <param name="output">Output.</param>
		public static void SerializeObjectToStream (object data, Stream output)
		{
			var serializeBinary = new MemoryStream ();
			var format = new BinaryFormatter ();
			format.Serialize (serializeBinary, data);
			output.Write (BitConverter.GetBytes (serializeBinary.Length), 0, 4);
			byte[] serializedByes = serializeBinary.ToArray ();
			output.Write (serializedByes, 0, serializedByes.Length);
		}

		/// <summary>
		/// Deserializes the object from stream.
		/// </summary>
		/// <returns>The object from stream.</returns>
		/// <param name="input">Input.</param>
		public static object DeserializeObjectFromStream (Stream input)
		{
			var rawInt32 = new byte[4];
			input.Read (rawInt32, 0, 4);
			int Length = BitConverter.ToInt32 (rawInt32, 0);
			var Data = new byte[Length];
			input.Read (Data, 0, Length);
			var binary = new BinaryFormatter ();
			object returnObj = binary.Deserialize (new MemoryStream (Data));
			return returnObj;
		}

		/// <summary>
		/// Writes the int32 to stream.
		/// </summary>
		/// <param name="input">Input.</param>
		/// <param name="output">Output.</param>
		public static void WriteInt32ToStream (int input, Stream output)
		{
			var data = BitConverter.GetBytes (input);
			output.Write (data, 0, 4);
		}

		/// <summary>
		/// Reads the int32 from stream.
		/// </summary>
		/// <returns>The int32 from stream.</returns>
		/// <param name="input">Input.</param>
		public static int ReadInt32FromStream (Stream input)
		{
			var data = new byte[4];
			input.Read (data, 0, 4);
			return BitConverter.ToInt32 (data, 0);
		}

		/// <summary>
		/// Writes the int64 to stream.
		/// </summary>
		/// <param name="input">Input.</param>
		/// <param name="output">Output.</param>
		public static void WriteInt64ToStream (long input, Stream output)
		{
			var data = BitConverter.GetBytes (input);
			output.Write (data, 0, 8);
		}

		/// <summary>
		/// Reads the int64 from stream.
		/// </summary>
		/// <returns>The int64 from stream.</returns>
		/// <param name="input">Input.</param>
		public static long ReadInt64FromStream (Stream input)
		{
			var data = new byte[8];
			input.Read (data, 0, 8);
			return BitConverter.ToInt64 (data, 0);
		}

		/// <summary>
		/// Writes the Uint32 to stream.
		/// </summary>
		/// <param name="input">Input.</param>
		/// <param name="output">Output.</param>
		public static void WriteUInt32ToStream (uint input, Stream output)
		{
			var data = BitConverter.GetBytes (input);
			output.Write (data, 0, 4);
		}

		/// <summary>
		/// Read UInt32 from Stream
		/// </summary>
		/// <returns>The U int32 from stream.</returns>
		/// <param name="input">Input.</param>
		public static uint ReadUInt32FromStream (Stream input)
		{
			var data = new byte[4];
			input.Read (data, 0, 4);
			return BitConverter.ToUInt32 (data, 0);
		}

		/// <summary>
		/// Writes the Uint32 to stream.
		/// </summary>
		/// <param name="input">Input.</param>
		/// <param name="output">Output.</param>
		public static void WriteUInt64ToStream (ulong input, Stream output)
		{
			var data = BitConverter.GetBytes (input);
			output.Write (data, 0, 8);
		}

		/// <summary>
		/// Read UInt32 from Stream
		/// </summary>
		/// <returns>The U int32 from stream.</returns>
		/// <param name="input">Input.</param>
		public static ulong ReadUInt64FromStream (Stream input)
		{
			var data = new byte[8];
			input.Read (data, 0, 8);
			return BitConverter.ToUInt64 (data, 0);
		}

		/// <summary>
		/// Writes a byte to stream.
		/// </summary>
		/// <param name="input">Input.</param>
		/// <param name="output">Output.</param>
		public static void WriteAByteToStream (byte input, Stream output)
		{
			output.Write (new [] { input }, 0, 1);
		}

		/// <summary>
		/// Read A Byte from Stream
		/// </summary>
		/// <returns>The byte from stream.</returns>
		/// <param name="input">Input.</param>
		public static byte ReadAByteFromStream (Stream input)
		{
			var data = new byte[1];
			input.Read (data, 0, 1);
			return data [0];
		}

		/// <summary>
		/// Write the Int32 Length and String to Stream
		/// </summary>
		/// <param name="input">Input.</param>
		/// <param name="output">Output.</param>
		public static void WriteStringToStream (string input, Stream output)
		{
			var data = Encoding.UTF8.GetBytes (input);
			output.Write (BitConverter.GetBytes (data.Length), 0, 4);
			output.Write (data, 0, data.Length);
		}

		/// <summary>
		/// Reads the Int32 Length and String from stream.
		/// </summary>
		/// <returns>The string from stream.</returns>
		/// <param name="input">Input.</param>
		public static string ReadStringFromStream (Stream input)
		{
			var rawInt32 = new byte[4];
			input.Read (rawInt32, 0, 4);
			var Length = BitConverter.ToInt32 (rawInt32, 0);
			var rawTextData = new byte[Length];
			input.Read (rawTextData, 0, Length);
			return Encoding.UTF8.GetString (rawTextData);
		}

		/// <summary>
		/// Writes the Int32 Length and Bytes to stream.
		/// </summary>
		/// <param name="data">Data.</param>
		/// <param name="input">Input.</param>
		public static void WriteBytesToStream (byte[] data, Stream input)
		{
			input.Write (BitConverter.GetBytes (data.Length), 0, 4);
			input.Write (data, 0, data.Length);
		}

		/// <summary>
		/// Read Int32 Length and then Read Bytes from Stream
		/// </summary>
		/// <returns>The bytes from stream.</returns>
		/// <param name="input">Input.</param>
		public static byte[] ReadBytesFromStream (Stream input)
		{
			var rawInt32 = new byte[4];
			input.Read (rawInt32, 0, 4);
			var Length = BitConverter.ToInt32 (rawInt32, 0);
			var data = new byte[Length];
			input.Read (data, 0, data.Length);
			return data;
		}

		/// <summary>
		/// Reads only bytes from stream.
		/// </summary>
		/// <returns>The only bytes from stream.</returns>
		/// <param name="input">Input.</param>
		/// <param name="size">Size.</param>
		public static byte[] ReadOnlyBytesFromStream (Stream input, int size)
		{
			var data = new byte[size];
			input.Read (data, 0, data.Length);
			return data;
		}

		/// <summary>
		/// Writes the only bytes to stream.
		/// </summary>
		/// <param name="output">Output.</param>
		/// <param name="data">Data.</param>
		public static void WriteOnlyBytesToStream (Stream output, byte[] data)
		{
			output.Write (data, 0, data.Length);
		}

		/// <summary>
		/// An unique function that clear and copy the remaining memory in one stream to a new stream
		/// essentially removing the unneeded data from the old stream.
		/// </summary>
		/// <param name="memStream">Mem stream.</param>
		/// <param name="length">Length.</param>
		public static void ClearAndCopyMemoryStream (ref MemoryStream memStream, int length)
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

