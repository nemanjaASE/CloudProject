
using Common.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace WebApp.Models
{
	public class SettingsViewModel
	{
		public Settings Settings { get; set; }
		public List<OwnedModelsGroup>? Models { get; set; }
		public class OwnedModelsGroup
		{
			public string GroupName { get; set; } 
			public List<SelectListItem> Models { get; set; } 
		}
		public string? NewRequirement { get; set; }
	}

}
