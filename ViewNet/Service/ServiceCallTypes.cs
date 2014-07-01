namespace ViewNet
{
	public enum ServiceCallTypes
	{
		Add = 0,
		Remove = 1,
		MismatchError = 2,
		Login = 3,
		LoginReject = 4,
		LoginAccept = 5,
		ServiceRead = 6,
		QueryPermissions = 7,
		RegisterAsNew = 8,
		RejectedNeedToRegister = 9,
		PermissionResponse = 11,
		RequestPermission = 12,
		DeniedPermission = 13,
		AcceptedPermission = 14
	}
}

