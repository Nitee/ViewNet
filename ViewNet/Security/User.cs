using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ViewNet
{
	public class User
	{
		public string Name { get; set; }

		public string Title { get; set; }
		// 64 bytes long key to identify the user login.
		public byte[] LocalKey { get; set; }

		public byte[] RemoteKey { get; set; }

		Permission[] userPermissionCache = new Permission[0];
		// Mainly for caching the permissions
		volatile bool userPermissionChanged;

		/// <summary>
		/// This is an extremely extensive property that require significant amount of iterations
		/// so it will cache the previous result in an attempt to prevent locking the system
		/// </summary>
		/// <value>The user permissions.</value>
		public Permission[] UserPermissions {
			get {
				if (!userPermissionChanged)
					lock (userPermissionCache)
						return userPermissionCache;

				var Permissions = new Dictionary<string, Permission> ();
				// This function basically enumerate through list of Groups and enumerate the permssion that
				// each group have and basically add it to the main Permission lists
				foreach (var group in UserGroup) {
					var enumerate = group.GroupPermission.GetEnumerator ();
					while (enumerate.MoveNext ()) {
						if (!Permissions.ContainsKey (enumerate.Current.Key))
							Permissions.Add (enumerate.Current.Key, enumerate.Current.Value);
						else if (!Permissions [enumerate.Current.Key].IsPermitted)
							Permissions [enumerate.Current.Key].IsPermitted = enumerate.Current.Value.IsPermitted;
					}
				}

				var upenumerate = userPermission.GetEnumerator ();
				while (upenumerate.MoveNext ()) {
					if (!Permissions.ContainsKey (upenumerate.Current.Key))
						Permissions.Add (upenumerate.Current.Key, upenumerate.Current.Value);
					else if (!Permissions [upenumerate.Current.Key].IsPermitted)
						Permissions [upenumerate.Current.Key].IsPermitted = upenumerate.Current.Value.IsPermitted;
				}
				var output = new Permission[Permissions.Values.Count];
				Permissions.Values.CopyTo (output, 0);
				userPermissionChanged = false;
				userPermissionCache = (Permission[])output.Clone ();
				return output;
			}
			set {
				userPermissionChanged = true;
				userPermission.Clear ();
				foreach (var item in value)
					userPermission.Add (item.Name, item);
			}
		}

		public Group[] UserGroup { get; set; }

		Dictionary<string, Permission> userPermission { get; set; }

		public FilePermission[] FilePermissions { get; set; }

		public DirectoryPermission[] DirectoryPermissions { get; set; }

		public User (string name)
		{
			Name = name;
		}

		public User (string name, string title)
		{
			Name = name;
			Title = title;
		}

		public User (string name, string title, Permission[] permits)
		{
			Name = name;
			Title = title;
			UserPermissions = permits;
		}

		public User (string name, Permission[] permits)
		{
			Name = name;
			Title = string.Empty;
			UserPermissions = permits;
		}

		public void AddPermission (Permission permit)
		{
			userPermissionChanged = true;
			if (userPermission.ContainsKey (permit.Name))
				userPermission [permit.Name].IsPermitted = permit.IsPermitted;
			else
				userPermission.Add (permit.Name, permit);
		}

		public static byte[] GeneratePasswordHash (string password)
		{
			var sha3Auth = SHA512.Create ();
			var hash = sha3Auth.ComputeHash (Encoding.UTF8.GetBytes (password));
			return hash;
		}
	}
}

