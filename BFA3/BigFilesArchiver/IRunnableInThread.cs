namespace BigFilesArchiver
{
    /// <summary>
    /// The basic for logic which we want to run in a thread
    /// </summary>
    interface IRunnableInThread
    {
        void DoWorkInThread();
    }
}
