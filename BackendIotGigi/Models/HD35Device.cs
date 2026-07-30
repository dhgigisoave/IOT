using System;
using System.Collections.Generic;
using System.Text;

namespace BackendIotGigi.Models
{
	public struct HD35Device
	{
		public HD35Device(string id, string name, string description, string location, DateTime createdAt)
		{
			Id = id;
			Name = name;
			Description = description;
			Location = location;
			CreatedAt = createdAt;
		}
		public string Id { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
		public string Location { get; set; }
		public DateTime CreatedAt { get; set; }
	}
}
