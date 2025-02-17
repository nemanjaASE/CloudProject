using System.Diagnostics;
using Microsoft.ServiceFabric.Services.Runtime;

namespace StudentService
{
    internal static class Program
    {
        private static void Main()
        {
            try
            {


                ServiceRuntime.RegisterServiceAsync("StudentServiceType",
                    context => new StudentService(context)).GetAwaiter().GetResult();

                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(StudentService).Name);
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}
