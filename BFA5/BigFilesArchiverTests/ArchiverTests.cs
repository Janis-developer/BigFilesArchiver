using Microsoft.VisualStudio.TestTools.UnitTesting;
using BigFilesArchiver;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BigFilesArchiver.Tests
{
    [TestClass()]
    public class ArchiverTests
    {
        static string dataPath = @"..\..\..\..\Data\";

        //string inFile = Path.Combine(dataPath, "in.pdf");
        static string inFile = Path.Combine(dataPath, "in.dat");
        static string outFile = inFile + ".bfa5.cgz";
        static string unzipFile = inFile + ".bfa5.unzipped";
        static int bufferSize = 100 * 1024 * 1024;
        static int differentBufferSize = 125 * 1024 * 1024;
        static int smallerBufferSize = 75 * 1024 * 1024;
        static uint procCount = (uint)Environment.ProcessorCount;

        [TestInitialize]
        public void Init()
        {
        }

        [TestMethod()]
        public async Task ZipByChunksTest()
        {
            await Archiver.ZipByChunksAsync(inFile, outFile, bufferSize);
        }
        
        [TestMethod()]
        public async Task ZipByChunks_UsingDifferentThreadNumTest()
        {
            await Archiver.ZipByChunksAsync(inFile, outFile, bufferSize, procCount / 2);
        }

        [TestMethod()]
        public async Task ZipByChunks_UsingSmallerBufferTest()
        {
            await Archiver.ZipByChunksAsync(inFile, outFile, smallerBufferSize);
        }

        [TestMethod()]
        public async Task ZipByChunks_UsingDifferentBufferTest()
        {
            await Archiver.ZipByChunksAsync(inFile, outFile, differentBufferSize);
        }


        [TestMethod()]
        public async Task UnzipFromChunksTest()
        {
            await Archiver.UnzipFromChunksAsync(outFile, unzipFile, bufferSize);
        }

        [TestMethod()]
        public async Task UnzipFromChunks_UsingDifferentBufferTest()
        {
            await Archiver.UnzipFromChunksAsync(outFile, unzipFile, differentBufferSize);
        }

        [TestMethod()]
        public async Task UnzipFromChunks_UsingSmallerBufferTest()
        {
            await Archiver.UnzipFromChunksAsync(outFile, unzipFile, smallerBufferSize);
        }

        [TestMethod()]
        public async Task UnzipFromChunks_UsingDifferentThreadNumTest()
        {
            await Archiver.UnzipFromChunksAsync(outFile, unzipFile, bufferSize, procCount + 1);
        }

        [TestMethod()]
        public async Task UnzipFromChunks_UsingSmallerBufferSizeAndThreadNumTest()
        {
            await Archiver.UnzipFromChunksAsync(outFile, unzipFile, smallerBufferSize, procCount - 1);
        }
        
    }
}