using System;
using System.Collections.Generic;
using System.Text;

namespace BackendIotGigi.Models
{
	internal class PackagingPayload : PayloadBase
	{
		public string SerialNumber { get; set; } = string.Empty;
	}
}
