using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Diagnostics;

namespace GZipTest
{
    public abstract class Zipper : IDisposable
    {
        private bool _blnEOF = false;
        private ManualResetEvent _thrStopEvent;
        private ManualResetEvent _thrNewMsgEvent;
        private ManualResetEvent _thrReadingQueueHasBlocksEvent = new ManualResetEvent(false);
        private ManualResetEvent _thrWritingQueueHasBlocksEvent = new ManualResetEvent(false);
        private ManualResetEvent _thrReadingQueueDeficitEvent = new ManualResetEvent(true);
        private ManualResetEvent _thrWritingQueueDeficitEvent = new ManualResetEvent(true);

        private int _intMaxBlockSize = 8388608; 
        private int _intThreadCount = Environment.ProcessorCount - 2;
        private int _intMaxBlocksCountInQueue; //= Environment.ProcessorCount * 10;
        private int _intLastReadedBlockId = 0;
        private int _intWritePendingBlockId = 0;

        private Dictionary<int, byte[]> _readingQueue = new Dictionary<int, byte[]>();
        private Dictionary<int, byte[]> _writingQueue = new Dictionary<int, byte[]>();
        private List<Object> _lstMessageQueue = new List<Object>();

        private DateTime _dtStartTime;
        private TimeSpan _tmReadingTime;
        private TimeSpan _tmTotalTime;
        private Decimal _decFileSize;
        private Decimal _decBeforeProcessBytesCount;
        private Decimal _decAfterProcessBytesCount;

        public Zipper(ManualResetEvent p_thrStopEvent, ManualResetEvent p_thrNewMsgEvent, List<Object> p_lstMessageQueue)
        {
            _thrStopEvent = p_thrStopEvent;
            _lstMessageQueue = p_lstMessageQueue;
            _thrNewMsgEvent = p_thrNewMsgEvent;

            float fltAvailiableMemory = new PerformanceCounter("Memory", "Available MBytes").NextValue() * 1048576;
            _intMaxBlocksCountInQueue = (int)(fltAvailiableMemory / _intMaxBlockSize);
        }

        public void ProcessFile(String p_strInputFilePath, String p_strOutputFilePath)
        {
            _dtStartTime = DateTime.Now;
            _decFileSize = (new FileInfo(p_strInputFilePath)).Length;
            _blnEOF = false;
            _intLastReadedBlockId = 0;
            _intWritePendingBlockId = 0;
            _decBeforeProcessBytesCount = 0;
            _decAfterProcessBytesCount = 0;
            try
            {
                Thread thrReadThread = new Thread(() => ReadSourceFile(p_strInputFilePath));
                thrReadThread.Priority = ThreadPriority.Highest;
                thrReadThread.Start();
                for (int i = 0; i < _intThreadCount; i++)
                {
                    Thread thrCompressingThread = new Thread(() => Process());
                    thrCompressingThread.Priority = ThreadPriority.Highest;
                    thrCompressingThread.Start();
                }
                Thread thrWriteThread = new Thread(() => WriteDestinationFile(p_strOutputFilePath));
                thrWriteThread.Priority = ThreadPriority.Highest;
                thrWriteThread.Start();
            }
            catch (Exception ex)
            {
                AddLog(ex);
                _thrStopEvent.Set();
            }
        }

        private void ReadSourceFile(String p_strInputFilePath)
        {
            try
            {
                AddLogInfo("File processing started. Please, wait.");
                using (FileStream ioSrcFileStream = new FileStream(p_strInputFilePath, FileMode.Open))
                {
                    using (BinaryReader ioSrcFileReader = new BinaryReader(ioSrcFileStream, Encoding.ASCII))
                    {
                        while (WaitHandle.WaitAny(new WaitHandle[] { _thrStopEvent, _thrReadingQueueDeficitEvent }) == 1)
                        {
                            if (ioSrcFileReader.PeekChar() < 0)
                            {
                                _intLastReadedBlockId--;
                                _blnEOF = true;
                                _tmReadingTime = DateTime.Now - _dtStartTime;
                                break;
                            }
                            byte[] arrReadBuffer = ReadBlock(ioSrcFileReader, _intMaxBlockSize);
                            AddBlock(_readingQueue, _intLastReadedBlockId, arrReadBuffer, _thrReadingQueueHasBlocksEvent, _thrReadingQueueDeficitEvent);
                            _intLastReadedBlockId++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog(ex);
                _thrStopEvent.Set();
            }
        }

        private void Process()
        {
            try
            {
                while (WaitHandle.WaitAny(new WaitHandle[] { _thrStopEvent, _thrReadingQueueHasBlocksEvent }) == 1)
                {
                    int intBlockIdToProcess = -1;
                    byte[] arrBlockToProcess = null;
                    Boolean blnIsLastBlock = false;
                    lock (_readingQueue)
                    {
                        if (_readingQueue.Count > 0)
                        {
                            intBlockIdToProcess = _readingQueue.Keys.Min();
                            arrBlockToProcess = _readingQueue[intBlockIdToProcess];
                            _readingQueue.Remove(intBlockIdToProcess);
                            if (_readingQueue.Count == 0)
                            {
                                _thrReadingQueueHasBlocksEvent.Reset();
                            }
                            if (_readingQueue.Count < _intMaxBlocksCountInQueue)
                            {
                                _thrReadingQueueDeficitEvent.Set();
                            }
                            _decBeforeProcessBytesCount += arrBlockToProcess.Length;
                        }
                        blnIsLastBlock = arrBlockToProcess == null && _blnEOF;
                    }
                    if (arrBlockToProcess != null)
                    {
                        byte[] arrBlockBuffer = ProcessBlock(arrBlockToProcess, _intMaxBlockSize);
                        AddBlock(_writingQueue, intBlockIdToProcess, arrBlockBuffer, _thrWritingQueueHasBlocksEvent, _thrWritingQueueDeficitEvent, () => _decAfterProcessBytesCount += arrBlockBuffer.Length);

                        int intProgress = (int)(_decBeforeProcessBytesCount / _decFileSize * 100);
                        int intCompression = (int)(_decAfterProcessBytesCount / _decBeforeProcessBytesCount * 100);
                        AddLogInfo($"Block {intBlockIdToProcess} Progress: {intProgress}% Compression: {intCompression}%");
                    }
                    if (blnIsLastBlock)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog(ex);
                _thrStopEvent.Set();
            }
        }

        private void WriteDestinationFile(String p_strDestinationPath)
        {
            try
            {
                using (FileStream ioDestinationFileStream = new FileStream(p_strDestinationPath, FileMode.CreateNew))
                {
                    using (BinaryWriter ioDestinationFileWriter = new BinaryWriter(ioDestinationFileStream))
                    {
                        while (!_blnEOF || _intWritePendingBlockId <= _intLastReadedBlockId)
                        {
                            if (WaitHandle.WaitAny(new WaitHandle[] { _thrStopEvent, _thrWritingQueueHasBlocksEvent }) == 0)
                            {
                                break;
                            }
                            byte[] arrBlock = null;
                            lock (_writingQueue)
                            {
                                if (_writingQueue.ContainsKey(_intWritePendingBlockId))
                                {
                                    arrBlock = _writingQueue[_intWritePendingBlockId];
                                    _writingQueue.Remove(_intWritePendingBlockId);
                                }
                                if (_writingQueue.Count == 0)
                                {
                                    _thrWritingQueueHasBlocksEvent.Reset();
                                }
                                if (_writingQueue.Count < _intMaxBlocksCountInQueue)
                                {
                                    _thrWritingQueueDeficitEvent.Set();
                                }
                            }
                            if (arrBlock != null)
                            {
                                WriteBlock(ioDestinationFileWriter, arrBlock);
                                _intWritePendingBlockId++;
                            }
                        }
                        _tmTotalTime = DateTime.Now - _dtStartTime;
                        AddLogInfo("Processing complete! Destination path is: " + p_strDestinationPath);
                        AddLogInfo($"Reading time: {_tmReadingTime}. Total time: {_tmTotalTime}");
                        _thrStopEvent.Set();
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog(ex);
                _thrStopEvent.Set();
            }
        }       

        protected abstract byte[] ReadBlock(BinaryReader p_ioSrcFileReader, int p_intMaxBlockSize);
        protected abstract byte[] ProcessBlock(byte[] arrBlock, int p_intMaxBlockSize);
        protected abstract void WriteBlock(BinaryWriter p_ioProcessedFileWriter, byte[] p_arrBlock);

        private void AddBlock(Dictionary<int, byte[]> p_dicAddTo, int p_intId, byte[] p_arrByteBlock,
            ManualResetEvent p_thrQueueHasBlockEvent, ManualResetEvent p_thrQueueDefictEvent,
            Action funcDoItWithLock = null)
        {
            if (p_intId == _intWritePendingBlockId || WaitHandle.WaitAny(new WaitHandle[] { _thrStopEvent, p_thrQueueDefictEvent }) == 1)
            {
                lock (p_dicAddTo)
                {
                    p_dicAddTo.Add(p_intId, p_arrByteBlock);
                    funcDoItWithLock?.Invoke();
                    p_thrQueueHasBlockEvent.Set();
                    if (p_dicAddTo.Count >= _intMaxBlocksCountInQueue)
                    {
                        p_thrQueueDefictEvent.Reset();
                    }
                }
            }
        }

        #region Messaging
        private void AddLog(Exception p_exLogMsg)
        {
            lock (_lstMessageQueue)
            {
                _lstMessageQueue.Add(p_exLogMsg);
                _thrNewMsgEvent.Set();
            }
        }

        private void AddLog(ConsoleColor p_cmdColor, Object p_objLogMsg)
        {
            lock (_lstMessageQueue)
            {
                _lstMessageQueue.Add(new ColoredMessage { Color = p_cmdColor, Msg = p_objLogMsg });
                _thrNewMsgEvent.Set();
            }
        }

        private void AddLogInfo(String p_strLogMsg)
        {
            AddLog(ConsoleColor.Green, p_strLogMsg);
        }
        #endregion

        #region IDisposable Support
        public void Dispose()
        {
            _thrReadingQueueHasBlocksEvent?.Close();
            _thrWritingQueueHasBlocksEvent?.Close();
            _thrReadingQueueDeficitEvent?.Close();
            _thrWritingQueueDeficitEvent?.Close();
        }
        #endregion
    }
}
