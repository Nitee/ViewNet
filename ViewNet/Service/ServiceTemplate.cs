using System;

namespace ViewNet
{
	/// <summary>
	/// Service template.
	/// </summary>
	public class ServiceTemplate : IService
	{
		ByteServiceStream managedStream { get; set; }

		volatile bool _isActive = false;

		public ServiceTemplate ()
		{
			managedStream = new ByteServiceStream (new Type[] {
				typeof(object)
			});
		}

		#region Don't Worry About Those Functions

		public byte[] Write ()
		{
			return managedStream.Write (65536);
		}

		public void Read (byte[] data)
		{
			managedStream.Read (data);
			Process ();
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

		#endregion

		private void Process ()
		{
			while (true) {
				var InputObject = managedStream.AttemptDequeueMessage ();
				if (InputObject == null)
					return;

				if (InputObject.GetType () == typeof(object))
					ProcessRandomObject (InputObject);
			}
		}

		#region Internal Process Functions

		private void ProcessRandomObject (object input)
		{

		}

		#endregion

	}
}

