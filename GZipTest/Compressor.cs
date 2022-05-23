using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    public class Compressor : Zipper
    {
        public Compressor(ManualResetEvent p_thrStopEvent, ManualResetEvent p_thrNewMsgEvent, List<Object> p_lstMessageQueue) : base(p_thrStopEvent, p_thrNewMsgEvent, p_lstMessageQueue) { }

        protected override byte[] ReadBlock(BinaryReader p_ioSrcFileReader, int p_intMaxBlockSize)
        {
            return p_ioSrcFileReader.ReadBytes(p_intMaxBlockSize);
        }

        protected override byte[] ProcessBlock(byte[] p_arrBlock, int p_intBlockSize)
        {
            using (MemoryStream ioSrcMemStream = new MemoryStream())
            {
                using (GZipStream gzsBlockComressingStream = new GZipStream(ioSrcMemStream, CompressionMode.Compress))
                {
                    gzsBlockComressingStream.Write(p_arrBlock, 0, p_arrBlock.Length);
                }
                return ioSrcMemStream.ToArray();
            }
        }

        protected override void WriteBlock(BinaryWriter p_ioProcessedFileWriter, byte[] p_arrBlock)
        {
            p_ioProcessedFileWriter.Write(p_arrBlock.Length);
            p_ioProcessedFileWriter.Write(p_arrBlock);
        }
    }
}
