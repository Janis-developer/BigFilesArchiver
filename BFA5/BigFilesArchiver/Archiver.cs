using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace BigFilesArchiver
{

    /// <summary>
    /// Main class to do zip/unzip by chunks.
    /// The version which is using async/await
    /// </summary>
    public static class Archiver
    {
        const int defaultChunkSize = 10 * 1024 * 1024;


        async public static Task ZipByChunksAsync(string inputName, string outputName, int bufferSize = 0, uint workersCount = 0)
        {
            await ManageThreadsAsync(inputName, outputName, ArchAction.Zip, bufferSize, workersCount);
        }


        async public static Task UnzipFromChunksAsync(string inputName, string outputName, int bufferSize = 0, uint workersCount = 0)
        {
            await ManageThreadsAsync(inputName, outputName, ArchAction.Unzip, bufferSize, workersCount);
        }


        async private static Task ManageThreadsAsync(string inputName, string outputName, ArchAction act, int bufferSize, uint workersCount = 0)
        {
            InOutBuffer[] buffers = null;
            Task[] workerTasks = null;

            try
            {
                General.Log($"Started '{inputName}' {act} into '{outputName}'...");

                if (bufferSize < defaultChunkSize)
                    bufferSize = defaultChunkSize;

                if (workersCount < 1)
                    workersCount = ChooseWorkersCount();

                General.Log($"buffer size {bufferSize}, workers count {workersCount}");

                //to feel good let's use 'using'

                // objects for input reading
                using FileStream inFileStream = File.OpenRead(inputName);
                ArchReader reader = new ArchReader(inFileStream, act);

                // objects for output writing
                using FileStream outFileStream = File.Create(outputName);
                ArchWriter writer = new ArchWriter(outFileStream, act);

                // create objects to do zipping/unzipping
                CreateBuffersAndWorkers(act, bufferSize, workersCount, out buffers, out ArchWorker[] workers, out workerTasks);

                await StartConveyor(reader, workers, workerTasks, buffers);

                int idx = 0;

                // as long as there are bytes read from input, keep conveyor
                // when no more input data, set this flag & wait till all therads are finihed.
                bool finishing = false;

                do
                {
                    ArchWorker worker = workers[idx];

                    if (worker == null)
                    {
                        General.Log($"all workers are finished");
                        break;
                    }

                    General.Log($"Wait for worker {idx} done, to start writting");
                    await workerTasks[idx];//wait till zipper idx has done

                    General.Log($"{idx} start writting");

                    writer.Buffer = buffers[idx];
                    Task writerTask = writer.WriteAsync();

                    if (!finishing)
                    {
                        General.Log($"{idx} start reading next portion");
                        reader.Buffer = buffers[idx];
                        Task readerTask = reader.ReadAsync();

                        General.Log($"{idx} need both In & Out buffers to be Ready to start next part zip/unzip");
                        await readerTask;

                        // check if have read anyting
                        if (reader.Buffer.BytesRead <= 0)
                        {
                            //have read all, and all is (being) processed
                            finishing = true;
                        }

                    }

                    await writerTask;

                    if (finishing)
                    {
                        workers[idx] = null;
                        workerTasks[idx] = null; //according to info from ms , no need to dispose a Task
                        buffers[idx] = null;
                    }
                    else
                    {
                        workerTasks[idx] = worker.DoWorkAsync();
                    }

                    //cause rotating, and output shall be in-order
                    if (++idx >= workers.Length)
                        idx = 0;

                } while (true);

                General.Log($"{act} finished successfuly.");
            }
            catch (Exception exc)
            {
                General.Log($"ManageThreads encountered an error {exc.Message} returning to caller.");
                throw;
            }
            finally
            {
                CleanUp(buffers, workerTasks);
            }
        }


        #region initialisation methods
        /// <summary>
        /// Creates all the objects required for the requested operation
        /// </summary>
        /// <param name="act"></param>
        /// <param name="bufferSize"></param>
        /// <param name="workersCount"></param>
        /// <param name="buffers"></param>
        /// <param name="workers"></param>
        /// <param name="workersTasks"></param>
        static void CreateBuffersAndWorkers(ArchAction act, int bufferSize, uint workersCount, out InOutBuffer[] buffers, out ArchWorker[] workers, out Task[] workerTasks)
        {
            CreateBuffers(workersCount, bufferSize, out buffers);

            CreateArchivers(buffers, act, out workers);

            workerTasks = new Task[workersCount];
        }


        /// <summary>
        /// Kick start of a zip/unzip process
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="workers"></param>
        /// <param name="workersTasks"></param>
        /// <param name="buffers"></param>
        async static Task StartConveyor(ArchReader reader, ArchWorker[] workers, Task[] workerTasks, InOutBuffer[] buffers)
        {
            Debug.Assert(workers.Length == buffers.Length);

            for (int idx = 0; idx < workers.Length; idx++)
            {
                ArchWorker worker = workers[idx];

                General.Log($"{idx} start reading");
                reader.Buffer = buffers[idx];
                //readerThread.haveWork.Set();
                //readerThread.notifyDone.WaitOne();
                await reader.ReadAsync();
                General.Log($"{idx} reading chunk complete, start zippin");

                workerTasks[idx] = worker.DoWorkAsync();
            }
        }


        /// <summary>
        /// Creates requested number of working buffers with given size
        /// </summary>
        /// <param name="workersCount"></param>
        /// <param name="bufferSize"></param>
        /// <returns>buffers as out parameter</returns>
        static void CreateBuffers(uint workersCount, int bufferSize, out InOutBuffer[] buffers)
        {
            General.Log($"Creating {workersCount} in/out buffers");

            InOutBuffer.ChunkSize = bufferSize;

            //buffers = Array.ConvertAll(new InOutBuffer[workersCount], _ => new InOutBuffer());

            buffers = new InOutBuffer[workersCount];

            for (int idx = 0; idx < buffers.Length; idx++)
                buffers[idx] = new InOutBuffer();
        }


        /// <summary>
        /// Detects optimal working threads count for this system
        /// </summary>
        /// <returns></returns>
        public static uint ChooseWorkersCount()
        {
            int procCount = Environment.ProcessorCount;
            General.Log($"Number Of Logical Processors: {procCount}");

            // it is unlikely to have single core nowdays, but just in case
            if (procCount < 2)
                // choose any number > 1 to demonstrate multithreading
                procCount = 2;

            return (uint)procCount;
        }


        /// <summary>
        /// Create those who do zip/unzip
        /// </summary>
        /// <param name="buffers"></param>
        /// <param name="act">zip or unzip</param>
        /// <param name="archivers">out param</param>
        /// <returns></returns>
        static void CreateArchivers(InOutBuffer[] buffers, ArchAction act, out ArchWorker[] archivers)
        {
            int count = buffers.Length;

            Debug.Assert(count > 0);

            General.Log($"Creating {count} (un)zippers");

            archivers = new ArchWorker[count];

            for (int i = 0; i < archivers.Length; i++)
                archivers[i] = new ArchWorker(inBuffer: buffers[i], outBuffer: buffers[i], act);
        }
        #endregion initialisation methods


        #region clean up 
        /// <summary>
        /// Let's clean
        /// </summary>
        /// <param name="buffers"></param>
        /// <param name="workers"></param>
        /// <param name="workersTasks"></param>
        private static void CleanUp(InOutBuffer[] buffers, Task[] workerTasks)
        {
            try
            {
                // actually, no need to dispose a Task (ms doc)
                if (workerTasks != null)
                {
                    foreach (var obj in workerTasks)
                        obj?.Dispose();
                }
                if (buffers != null)
                {
                    foreach (var obj in buffers)
                        obj?.Dispose();
                }
            }
            catch (Exception exc2)
            {
                General.Log($"Clean up failed: {exc2.Message}");
            }
        }
        #endregion clean up 
    }
}
