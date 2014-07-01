using System;
using IntXLib;
using System.IO;

namespace ViewNet
{
	/// <summary>
	/// Represents the Diffie-Hellman algorithm.
	/// </summary>
	public class DiffieHellman : IDisposable
	{

		#region - Util -

		Random _weakRND = new Random ();

		#endregion

		#region - Fields -

		/// <summary>
		/// The number of bytes to generate.
		/// </summary>
		int bytes = 65536;
		/// <summary>
		/// The shared prime.
		/// </summary>
		IntX p;
		/// <summary>
		/// The shared base.
		/// </summary>
		IntX g;
		/// <summary>
		/// The secret number
		/// </summary>
		uint a;
		/// <summary>
		/// The final key.
		/// </summary>
		public IntX S;

		#endregion

		#region - Properties -

		/// <summary>
		/// Gets the final key to use for encryption.
		/// </summary>
		public byte[] Key {
			get;
			set;
		}

		#endregion

		#region - Ctor -

		public DiffieHellman ()
		{
		}

		public DiffieHellman (int bytesize)
		{
			bytes = bytesize;
		}

		~DiffieHellman ()
		{
			Dispose ();
		}

		#endregion

		#region - Implementation Methods -

		#region Flow

		IntX GeneratePrime ()
		{
			int limit = bytes;
			if (limit < 4)
				limit = 4;
			var raw = new byte[limit];
			_weakRND.NextBytes (raw);
			var newInt = new IntX (DigitConverter.FromBytes (raw), false);
			return newInt;
		}

		/// <summary>
		/// Generates a request packet.
		/// </summary>
		/// <returns></returns>
		public byte[] GenerateRequest ()
		{
			// Generate the parameters.
			var raw = new byte[bytes * 8];
			_weakRND.NextBytes (raw);
			a = (uint)_weakRND.Next ((bytes * 8) / 4 * 3, bytes * 8);
			p = GeneratePrime ();
			g = new IntX (DigitConverter.FromBytes (raw), false);
			IntX A = IntX.Pow (g, a, MultiplyMode.AutoFht);
			A = IntX.Modulo (A, p, DivideMode.AutoNewton);

			var memStream = new MemoryStream ();
			uint[] temp;
			bool ignoreThis;

			// Get Raw IntX Data
			g.GetInternalState (out temp, out ignoreThis);
			var gData = DigitConverter.ToBytes (temp);
			p.GetInternalState (out temp, out ignoreThis);
			var pData = DigitConverter.ToBytes (temp);
			A.GetInternalState (out temp, out ignoreThis);
			var AData = DigitConverter.ToBytes (temp);
			// Write Length to Stream
			memStream.Write (BitConverter.GetBytes (gData.Length), 0, 4);
			memStream.Write (BitConverter.GetBytes (pData.Length), 0, 4);
			memStream.Write (BitConverter.GetBytes (AData.Length), 0, 4);

			// Write Data to Stream
			memStream.Write (gData, 0, gData.Length);
			memStream.Write (pData, 0, pData.Length);
			memStream.Write (AData, 0, AData.Length);

			var finalDataSend = memStream.ToArray ();
			memStream.Dispose ();
			return finalDataSend;
		}

		void GetKeyData ()
		{
			uint[] digits;
			bool fakebool;
			S.GetInternalState (out digits, out fakebool);
			Key = DigitConverter.ToBytes (digits);
		}

		/// <summary>
		/// Generate a response packet.
		/// </summary>
		/// <param name="request">The string representation of the request.</param>
		/// <returns></returns>
		public byte[] GenerateResponse (byte[] request)
		{
			var instream = new MemoryStream (request);
			var temp = new byte[4];
			instream.Read (temp, 0, temp.Length);
			int gLength = BitConverter.ToInt32 (temp, 0);
			instream.Read (temp, 0, temp.Length);
			int pLength = BitConverter.ToInt32 (temp, 0);
			instream.Read (temp, 0, temp.Length);
			int ALength = BitConverter.ToInt32 (temp, 0);

			temp = new byte[gLength];
			instream.Read (temp, 0, gLength);
			g = new IntX (DigitConverter.FromBytes (temp), false);
			temp = new byte[pLength];
			instream.Read (temp, 0, pLength);
			p = new IntX (DigitConverter.FromBytes (temp), false);
			temp = new byte[ALength];
			instream.Read (temp, 0, ALength);
			var A = new IntX (DigitConverter.FromBytes (temp), false);
			// Generate the parameters.

			a = (uint)_weakRND.Next (bytes);
			IntX B = IntX.Pow (g, a);
			B = IntX.Modulo (B, p, DivideMode.AutoNewton);

			var memStream = new MemoryStream ();
			bool ignoreThis;

			// Get Raw IntX Data
			uint[] tempDigits;
			B.GetInternalState (out tempDigits, out ignoreThis);
			var BData = DigitConverter.ToBytes (tempDigits);

			memStream.Write (BData, 0, BData.Length);

			var finalDataSend = memStream.ToArray ();
			memStream.Dispose ();

			// Got the key!!! HOORAY!
			S = IntX.Pow (A, a);
			S = IntX.Modulo (S, p, DivideMode.AutoNewton);
			GetKeyData ();
			return finalDataSend;
		}

		/// <summary>
		/// Generates the key after a response is received.
		/// </summary>
		/// <param name="response">The string representation of the response.</param>
		public void HandleResponse (byte[] response)
		{
			var B = new IntX (DigitConverter.FromBytes (response), false);

			S = IntX.Pow (B, a);
			S = IntX.Modulo (S, p, DivideMode.AutoNewton);
			GetKeyData ();
			Dispose ();
		}

		#endregion

		#endregion

		#region IDisposable Members

		/// <summary>
		/// Ends the calculation. The key will still be available.
		/// </summary>
		public void Dispose ()
		{
			p = null;
			g = null;
			GC.Collect ();
		}

		#endregion

	}
}

