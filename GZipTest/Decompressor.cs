using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipTest
{
    public class Decompressor : Zipper
    {
        public Decompressor(ManualResetEvent p_thrStopEvent, ManualResetEvent p_thrNewMsgEvent, List<Object> p_lstMessageQueue) : base(p_thrStopEvent, p_thrNewMsgEvent, p_lstMessageQueue) { }

        protected override byte[] ReadBlock(BinaryReader p_ioSrcFileReader, int p_intMaxBlockSize)
        {
            int intSizeOfBlock = p_ioSrcFileReader.ReadInt32();
            return p_ioSrcFileReader.ReadBytes(intSizeOfBlock);
        }

        protected override byte[] ProcessBlock(byte[] p_arrBlock, int p_intBlockSize)
        {
            int intBytesReaded;
            byte[] arrBlockBuffer = new byte[p_intBlockSize];
            using (MemoryStream ioSrcMemStream = new MemoryStream(p_arrBlock))
            {
                using (GZipStream gzsBlockDecompressingStream = new GZipStream(ioSrcMemStream, CompressionMode.Decompress))
                {
                    intBytesReaded = gzsBlockDecompressingStream.Read(arrBlockBuffer, 0, p_intBlockSize);
                }
            }
            byte[] arrCopyBuffer = new byte[intBytesReaded];
            Array.Copy(arrBlockBuffer, arrCopyBuffer, intBytesReaded);

            return arrCopyBuffer;
        }

        protected override void WriteBlock(BinaryWriter p_ioProcessedFileWriter, byte[] p_arrBlock)
        {
            p_ioProcessedFileWriter.Write(p_arrBlock);
        }
    }
}
