using Common.Models;
using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Interfaces
{
	public interface IProcessingTimeEstimator : IService
	{
		Task<double> EstimateTime(int textLength);
		Task LogProcessingTime(int textLength, double time);
	}
}
