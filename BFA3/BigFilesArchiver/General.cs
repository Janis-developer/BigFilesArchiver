using System;
using System.Threading;

namespace BigFilesArchiver
{
	public static class General
	{
		public static void Log(string msg)
		{
			DateTime now = DateTime.Now;
			string message = $"{now.Second,0:00}:{now.Millisecond,0:000} TH{Thread.CurrentThread.ManagedThreadId,0:000}: " + msg;
			System.Diagnostics.Trace.WriteLine(message);
#if DEBUG
			//System.Console.WriteLine(message);
#endif
		}
	}

}
