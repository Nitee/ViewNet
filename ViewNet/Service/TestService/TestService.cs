using System;

namespace ViewNet
{
	/// <summary>
	/// Service template.
	/// </summary>
	public class TestService : IService
	{
		ByteServiceStream managedStream { get; set; }

		volatile bool _isActive;

		public string PreviousMessage { get; set; }

		public TestService ()
		{
			managedStream = new ByteServiceStream (new [] {
				typeof(TestServiceCall)
			});
		}

		public void TestCall (string message)
		{
			managedStream.EnqueueMessage (new TestServiceCall (message));
		}

		public byte[] Write ()
		{
			return managedStream.Write (65536);
		}

		public void Read (byte[] data)
		{
			Console.WriteLine ("Reading");
			managedStream.Read (data);
			Process ();
		}

		void Process ()
		{
			while (true) {
				var message = managedStream.AttemptDequeueMessage ();
				if (message == null) {
					Console.WriteLine ("Failed");
					return;
				}
				if (message.GetType () == typeof(TestServiceCall)) {
					var call = (TestServiceCall)message;
					Console.WriteLine (message);
					if (call.Message.IndexOf ("Post:", StringComparison.CurrentCulture) != 0) {
						managedStream.EnqueueMessage (new TestServiceCall ("Post:" + call.Message));
					}
					PreviousMessage = call.Message;
				}
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

	public class TestServiceCall
	{
		public TestServiceCall ()
		{
		}

		public TestServiceCall (string message)
		{
			Message = message;
		}

		public string Message { get; set; }
	}
}

