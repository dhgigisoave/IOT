using System;
using System.Collections.Generic;
using System.Text;

namespace BackendIotGigi.Models
{
	public class DeviceClaimReq
	{
		public DeviceClaimReq() { }
		public DeviceClaimReq(string OTP, string userId) { this.OTP = OTP; this.userId = userId; }
		public string OTP { get; set; } = string.Empty;
		public string userId { get; set; } = string.Empty;
	}

	public struct DeviceClaimResp
	{
		public DeviceClaimResp() { }
		public DeviceClaimResp(HD35Device device, string userId) { this.device = device; this.userId = userId; }
		public HD35Device device { get; set; } = new HD35Device();
		public string userId { get; set; } = string.Empty;
	} 
}
