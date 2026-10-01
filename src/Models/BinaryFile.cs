using System;
using System.IO;

namespace SourceGit.Models
{
    public class BinaryFile : IDisposable
    {
        public long Size
        {
            get => _size;
        }

        public BinaryFile(string file, bool needDeleteFile = false)
        {
            _filePath = file;
            _needDeleteFile = needDeleteFile;

            if (File.Exists(_filePath))
            {
                _size = new FileInfo(_filePath).Length;
                _reader = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BUFFER_SIZE, FileOptions.RandomAccess);
                _readedStart = 0;
                _readedEnd = Math.Min(_size, BUFFER_SIZE);

                if (_size > 0)
                {
                    _reader.Seek(_readedStart, SeekOrigin.Begin);
                    _reader.ReadExactly(_buffer, 0, (int)_readedEnd);
                }
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _reader = null;

            if (_needDeleteFile && File.Exists(_filePath))
                File.Delete(_filePath);
        }

        public ArraySegment<byte> Read(long offset, long length)
        {
            if (_reader == null || _size == 0 || offset >= _size)
                return Array.Empty<byte>();

            if (length > 8192)
                length = 8192;

            if (offset + length > _size)
                length = _size - offset;

            if (_readedStart <= offset && _readedEnd >= offset + length)
                return new ArraySegment<byte>(_buffer, (int)(offset - _readedStart), (int)length);

            _readedStart = (Math.Max(0, offset - 2048) / 1024) * 1024;
            _readedEnd = Math.Min(_readedStart + BUFFER_SIZE, _size);

            _reader.Seek(_readedStart, SeekOrigin.Begin);
            _reader.ReadExactly(_buffer, 0, (int)(_readedEnd - _readedStart));

            return new ArraySegment<byte>(_buffer, (int)(offset - _readedStart), (int)length);
        }

        private const int BUFFER_SIZE = 16384;

        private string _filePath = string.Empty;
        private bool _needDeleteFile = false;
        private FileStream _reader = null;
        private long _size = 0;
        private long _readedStart = 0;
        private long _readedEnd = 0;
        private byte[] _buffer = new byte[BUFFER_SIZE];
    }
}
