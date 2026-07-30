using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace BackendIotGigi.Models
{
	internal class DeviceForUserReq
	{
		[JsonPropertyName("userId")]
		public string UserId { get; set; } = string.Empty;		
	}
}
