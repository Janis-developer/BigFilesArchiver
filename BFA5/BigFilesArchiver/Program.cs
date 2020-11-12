using System;
using System.Threading.Tasks;

namespace BigFilesArchiver
{
    class Program
	{
		async static Task<int> Main(string[] args)
		{
			try
			{
				if (args.Length != 3)
				{
					ShowUsage();
					return 1;
				}

				const int bufferSize = 100 * 1024 * 1024;

				string inFile;
				string zippedFile;
				string unzipFile;

				if (args[0].ToLower() == "compress")
				{
					inFile = args[1];
					zippedFile = args[2];

					Console.WriteLine($"Archiving {inFile}");
					Console.WriteLine($"Start : {DateTime.Now}");

					await Archiver.ZipByChunksAsync(inFile, zippedFile, bufferSize);

					Console.WriteLine($"Finish : {DateTime.Now}");
					Console.WriteLine($"BigFilesArchiver Done {zippedFile}.");
				}
				else if (args[0].ToLower() == "decompress")
				{
					zippedFile = args[1];
					unzipFile = args[2];

					Console.WriteLine($"Unzipping {zippedFile}");
					Console.WriteLine($"Start : {DateTime.Now}");

					await Archiver.UnzipFromChunksAsync(zippedFile, unzipFile, bufferSize);

					Console.WriteLine($"Finish : {DateTime.Now}");
					Console.WriteLine($"BigFilesArchiver Done {unzipFile}.");
				}
				else
                {
					ShowUsage();
					return 1;
				}
			}
			catch(Exception ex)
            {
				Console.WriteLine("Applicaiton produced an error:");
				Console.WriteLine(ex.Message);
				ShowUsage();
				return 1;
            }

			return 0;
		}

        private static void ShowUsage()
        {
			Console.WriteLine();
			Console.WriteLine("BigFilesArchiver version 1");
			Console.WriteLine("This program demonstartes files archieving by chunks (test)");
			Console.WriteLine($"Current directory for relative path name is {Environment.CurrentDirectory}");
			Console.WriteLine();

			Console.WriteLine("Use with following arguments:");
			Console.WriteLine("compressing: ProgramName compress [original file name] [archive file name to be created]");
			Console.WriteLine("decompressing: ProgramName decompress [archive file name] [decompressed file name to be created]");
			Console.WriteLine();
			//Console.ReadKey();
			//Console.WriteLine("Press any key to exit");
		}
	}

}
